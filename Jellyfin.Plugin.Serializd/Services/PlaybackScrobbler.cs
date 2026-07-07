using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Serializd.API;
using Jellyfin.Plugin.Serializd.API.Exceptions;
using Jellyfin.Plugin.Serializd.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Serializd.Services
{
    public class PlaybackScrobbler : IHostedService
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<PlaybackScrobbler> _logger;
        private readonly SerializdApi _api;
        private readonly ConcurrentDictionary<string, Guid> _lastScrobbled = new();
        private readonly ConcurrentDictionary<string, DateTime> _nextTry = new();

        public PlaybackScrobbler(
            ISessionManager sessionManager,
            ILogger<PlaybackScrobbler> logger,
            SerializdApi api)
        {
            _sessionManager = sessionManager;
            _logger = logger;
            _api = api;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            return Task.CompletedTask;
        }

        private static bool CanBeScrobbled(UserConfig config, PlaybackProgressEventArgs e)
        {
            if (e.MediaInfo?.Type != BaseItemKind.Episode || !config.ScrobbleShows)
            {
                return false;
            }

            var runtime = e.MediaInfo.RunTimeTicks;
            if (runtime is > 0)
            {
                var percentage = e.PlaybackPositionTicks / (float)runtime * 100f;
                if (percentage < config.ScrobblePercentage)
                {
                    return false;
                }

                if (runtime < TimeSpan.FromMinutes(config.MinLength).Ticks)
                {
                    return false;
                }
            }

            return true;
        }

        private async void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        {
            try
            {
                var sessionId = e.Session?.Id;
                if (sessionId == null)
                {
                    return;
                }

                var timeout = SerializdPlugin.Instance?.Configuration.GetByGuid(e.Session!.UserId)?.ScrobbleTimeout ?? 30;
                if (_nextTry.TryGetValue(sessionId, out var next) && DateTime.UtcNow < next)
                {
                    return;
                }

                _nextTry[sessionId] = DateTime.UtcNow.AddSeconds(timeout);
                await ScrobbleSession(e).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during playback-progress scrobble");
            }
        }

        private async void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
        {
            try
            {
                await ScrobbleSession(e).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during playback-stopped scrobble");
            }
            finally
            {
                var sessionId = e.Session?.Id;
                if (sessionId != null)
                {
                    _lastScrobbled.TryRemove(sessionId, out _);
                    _nextTry.TryRemove(sessionId, out _);
                }
            }
        }

        private async Task ScrobbleSession(PlaybackProgressEventArgs e)
        {
            if (e.Session == null)
            {
                return;
            }

            var userId = e.Session.UserId;
            var userConfig = SerializdPlugin.Instance?.Configuration.GetByGuid(userId);
            if (userConfig == null || string.IsNullOrEmpty(userConfig.UserToken))
            {
                return;
            }

            if (!CanBeScrobbled(userConfig, e))
            {
                return;
            }

            if (e.Item is not Episode episode)
            {
                return;
            }

            if (_lastScrobbled.TryGetValue(e.Session.Id, out var last) && last == episode.Id)
            {
                return;
            }

            var tmdbRaw = episode.Series?.GetProviderId(MetadataProvider.Tmdb);
            if (string.IsNullOrEmpty(tmdbRaw)
                || !int.TryParse(tmdbRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var showId))
            {
                _logger.LogInformation(
                    "Skipping scrobble for {Name}: parent series has no TMDB id",
                    episode.Name);
                return;
            }

            if (episode.ParentIndexNumber is not int seasonNumber || episode.IndexNumber is not int episodeNumber)
            {
                _logger.LogInformation("Skipping scrobble for {Name}: missing season/episode number", episode.Name);
                return;
            }

            async Task<bool> AttemptScrobble()
            {
                var seasonId = await _api.ResolveSeasonIdAsync(showId, seasonNumber, userConfig.UserToken).ConfigureAwait(false);
                if (seasonId is null)
                {
                    return false;
                }

                if (!userConfig.LogToDiary)
                {
                    return await _api.LogEpisodeAsync(showId, seasonId.Value, episodeNumber, userConfig.UserToken).ConfigureAwait(false);
                }

                await _api.LogEpisodeAsync(showId, seasonId.Value, episodeNumber, userConfig.UserToken).ConfigureAwait(false);
                return await _api.LogEpisodeToDiaryAsync(showId, seasonId.Value, episodeNumber, DateTime.UtcNow, userConfig.UserToken).ConfigureAwait(false);
            }

            try
            {
                bool success;
                try
                {
                    success = await AttemptScrobble().ConfigureAwait(false);
                }
                catch (InvalidTokenException)
                {
                    if (!await TryReauthenticate(userConfig, e.Session.UserName).ConfigureAwait(false))
                    {
                        return;
                    }

                    success = await AttemptScrobble().ConfigureAwait(false);
                }

                if (success)
                {
                    _lastScrobbled[e.Session.Id] = episode.Id;
                    _logger.LogInformation(
                        "Scrobbled {Series} S{Season}E{Episode} to Serializd for {User}",
                        episode.Series?.Name,
                        seasonNumber,
                        episodeNumber,
                        e.Session.UserName);
                }
            }
            catch (InvalidTokenException)
            {
                _logger.LogWarning("Serializd re-authentication for {User} failed; clearing stored credentials", e.Session.UserName);
                SerializdPlugin.Instance?.Configuration.ClearCredentials(userConfig.Id);
                SerializdPlugin.Instance?.SaveConfiguration();
            }
        }

        private async Task<bool> TryReauthenticate(UserConfig userConfig, string? userName)
        {
            var password = SecretProtector.Unprotect(userConfig.ProtectedPassword);
            if (string.IsNullOrEmpty(userConfig.Email) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Serializd token for {User} expired and no stored credentials are available to refresh it; clearing", userName);
                SerializdPlugin.Instance?.Configuration.ClearCredentials(userConfig.Id);
                SerializdPlugin.Instance?.SaveConfiguration();
                return false;
            }

            var login = await _api.LoginAsync(userConfig.Email, password).ConfigureAwait(false);
            if (login?.Token is null || string.IsNullOrEmpty(login.Token))
            {
                _logger.LogWarning("Serializd re-login for {User} failed; clearing stored credentials", userName);
                SerializdPlugin.Instance?.Configuration.ClearCredentials(userConfig.Id);
                SerializdPlugin.Instance?.SaveConfiguration();
                return false;
            }

            SerializdPlugin.Instance?.Configuration.SetToken(userConfig.Id, login.Username ?? userConfig.Username, login.Token);
            SerializdPlugin.Instance?.SaveConfiguration();
            _logger.LogInformation("Refreshed Serializd token for {User}", userName);
            return true;
        }
    }
}
