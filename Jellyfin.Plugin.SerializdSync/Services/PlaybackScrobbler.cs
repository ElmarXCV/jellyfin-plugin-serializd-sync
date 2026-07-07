using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SerializdSync.API;
using Jellyfin.Plugin.SerializdSync.API.Exceptions;
using Jellyfin.Plugin.SerializdSync.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SerializdSync.Services
{
    public class PlaybackScrobbler : IHostedService
    {
        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger<PlaybackScrobbler> _logger;
        private readonly SerializdApi _api;
        private readonly ConcurrentDictionary<string, Guid> _lastScrobbled = new();
        private readonly ConcurrentDictionary<string, DateTime> _nextTry = new();

        public PlaybackScrobbler(
            ISessionManager sessionManager,
            IUserDataManager userDataManager,
            ILogger<PlaybackScrobbler> logger,
            SerializdApi api)
        {
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _logger = logger;
            _api = api;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _userDataManager.UserDataSaved += OnUserDataSaved;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _userDataManager.UserDataSaved -= OnUserDataSaved;
            return Task.CompletedTask;
        }

        private static bool CanBeScrobbled(UserConfig config, PlaybackProgressEventArgs e, bool isStopped)
        {
            if (e.MediaInfo?.Type != BaseItemKind.Episode || !config.ScrobbleShows)
            {
                return false;
            }

            var runtime = e.MediaInfo.RunTimeTicks;
            if (runtime is not > 0)
            {
                return isStopped;
            }

            var percentage = e.PlaybackPositionTicks / (float)runtime * 100f;
            if (percentage < config.ScrobblePercentage)
            {
                return false;
            }

            return runtime >= TimeSpan.FromMinutes(config.MinLength).Ticks;
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
                await ScrobbleSession(e, false).ConfigureAwait(false);
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
                await ScrobbleSession(e, true).ConfigureAwait(false);
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

        private async void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
        {
            try
            {
                if (e.SaveReason != UserDataSaveReason.TogglePlayed)
                {
                    return;
                }

                if (e.Item is not Episode episode)
                {
                    return;
                }

                var userConfig = SerializdPlugin.Instance?.Configuration.GetByGuid(e.UserId);
                if (userConfig == null || string.IsNullOrEmpty(userConfig.UserToken) || !userConfig.ScrobbleShows)
                {
                    return;
                }

                if (!TryGetEpisodeIds(episode, out var showId, out var seasonNumber, out var episodeNumber))
                {
                    return;
                }

                var played = e.UserData.Played;
                var ok = await RunWithReauth(
                    userConfig,
                    null,
                    token => played
                        ? LogEpisodeCore(showId, seasonNumber, episodeNumber, userConfig, token)
                        : UnlogEpisodeCore(showId, seasonNumber, episodeNumber, token)).ConfigureAwait(false);

                if (ok)
                {
                    _logger.LogInformation(
                        "{Action} {Series} S{Season}E{Episode} on Serializd (manual)",
                        played ? "Logged" : "Unlogged",
                        episode.Series?.Name,
                        seasonNumber,
                        episodeNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error handling user-data change");
            }
        }

        private async Task ScrobbleSession(PlaybackProgressEventArgs e, bool isStopped)
        {
            if (e.Session == null)
            {
                return;
            }

            var userConfig = SerializdPlugin.Instance?.Configuration.GetByGuid(e.Session.UserId);
            if (userConfig == null || string.IsNullOrEmpty(userConfig.UserToken))
            {
                return;
            }

            if (!CanBeScrobbled(userConfig, e, isStopped))
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

            if (!TryGetEpisodeIds(episode, out var showId, out var seasonNumber, out var episodeNumber))
            {
                return;
            }

            var ok = await RunWithReauth(
                userConfig,
                e.Session.UserName,
                token => LogEpisodeCore(showId, seasonNumber, episodeNumber, userConfig, token)).ConfigureAwait(false);

            if (ok)
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

        private bool TryGetEpisodeIds(Episode episode, out int showId, out int seasonNumber, out int episodeNumber)
        {
            showId = 0;
            seasonNumber = 0;
            episodeNumber = 0;

            var tmdbRaw = episode.Series?.GetProviderId(MetadataProvider.Tmdb);
            if (string.IsNullOrEmpty(tmdbRaw)
                || !int.TryParse(tmdbRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out showId))
            {
                _logger.LogInformation("Skipping {Name}: parent series has no TMDB id", episode.Name);
                return false;
            }

            if (episode.ParentIndexNumber is not int season || episode.IndexNumber is not int number)
            {
                _logger.LogInformation("Skipping {Name}: missing season/episode number", episode.Name);
                return false;
            }

            seasonNumber = season;
            episodeNumber = number;
            return true;
        }

        private async Task<bool> LogEpisodeCore(int showId, int seasonNumber, int episodeNumber, UserConfig userConfig, string token)
        {
            var seasonId = await _api.ResolveSeasonIdAsync(showId, seasonNumber, token).ConfigureAwait(false);
            if (seasonId is null)
            {
                return false;
            }

            if (!userConfig.LogToDiary)
            {
                return await _api.LogEpisodeAsync(showId, seasonId.Value, episodeNumber, token).ConfigureAwait(false);
            }

            await _api.LogEpisodeAsync(showId, seasonId.Value, episodeNumber, token).ConfigureAwait(false);
            return await _api.LogEpisodeToDiaryAsync(showId, seasonId.Value, episodeNumber, DateTime.UtcNow, token).ConfigureAwait(false);
        }

        private async Task<bool> UnlogEpisodeCore(int showId, int seasonNumber, int episodeNumber, string token)
        {
            var seasonId = await _api.ResolveSeasonIdAsync(showId, seasonNumber, token).ConfigureAwait(false);
            if (seasonId is null)
            {
                return false;
            }

            return await _api.UnlogEpisodeAsync(showId, seasonId.Value, episodeNumber, token).ConfigureAwait(false);
        }

        private async Task<bool> RunWithReauth(UserConfig userConfig, string? userName, Func<string, Task<bool>> action)
        {
            try
            {
                try
                {
                    return await action(userConfig.UserToken).ConfigureAwait(false);
                }
                catch (InvalidTokenException)
                {
                    if (!await TryReauthenticate(userConfig, userName).ConfigureAwait(false))
                    {
                        return false;
                    }

                    return await action(userConfig.UserToken).ConfigureAwait(false);
                }
            }
            catch (InvalidTokenException)
            {
                _logger.LogWarning("Serializd re-authentication for {User} failed; clearing stored credentials", userName);
                SerializdPlugin.Instance?.Configuration.ClearCredentials(userConfig.Id);
                SerializdPlugin.Instance?.SaveConfiguration();
                return false;
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
