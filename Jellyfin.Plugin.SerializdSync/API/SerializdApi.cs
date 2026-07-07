using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SerializdSync.API.Exceptions;
using Jellyfin.Plugin.SerializdSync.API.Objects;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SerializdSync.API
{
    public class SerializdApi
    {
        public const string BaseUrl = "https://serializd.onrender.com/api";

        public const string FrontPageUrl = "https://www.serializd.com";
        public const string AppId = "serializd_vercel";

        private readonly ILogger<SerializdApi> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonOptions;

        private readonly ConcurrentDictionary<(int Show, int Season), int> _seasonIdCache = new();

        public SerializdApi(ILogger<SerializdApi> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            var body = new LoginRequest { Email = email, Password = password };
            using var request = CreateRequest(HttpMethod.Post, "/login", null, body);
            var response = await Send(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Serializd login failed with status {Status}", response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<LoginResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<int?> ResolveSeasonIdAsync(int showId, int seasonNumber, string token, CancellationToken cancellationToken = default)
        {
            if (_seasonIdCache.TryGetValue((showId, seasonNumber), out var cached))
            {
                return cached;
            }

            using var request = CreateRequest(HttpMethod.Get, $"/show/{showId}/season/{seasonNumber}", token);
            var response = await Send(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidTokenException("Serializd rejected the stored access token");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to resolve season {Season} of show {Show}: status {Status}",
                    seasonNumber,
                    showId,
                    response.StatusCode);
                return null;
            }

            var season = await response.Content
                .ReadFromJsonAsync<SeasonResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (season?.SeasonId is not int seasonId)
            {
                _logger.LogWarning("Serializd returned no season id for show {Show} season {Season}", showId, seasonNumber);
                return null;
            }

            _seasonIdCache[(showId, seasonNumber)] = seasonId;
            return seasonId;
        }

        public async Task<bool> LogEpisodeAsync(int showId, int seasonId, int episodeNumber, string token, CancellationToken cancellationToken = default)
        {
            var body = new LogEpisodesRequest
            {
                ShowId = showId,
                SeasonId = seasonId,
                EpisodeNumbers = new[] { episodeNumber }
            };

            using var request = CreateRequest(HttpMethod.Post, "/episode_log/add", token, body);
            var response = await Send(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidTokenException("Serializd rejected the stored access token");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to log S{Season}E{Episode} of show {Show}: status {Status}",
                    seasonId,
                    episodeNumber,
                    showId,
                    response.StatusCode);
                return false;
            }

            return true;
        }

        public async Task<bool> LogEpisodeToDiaryAsync(int showId, int seasonId, int episodeNumber, DateTime watchedUtc, string token, CancellationToken cancellationToken = default)
        {
            var body = new DiaryEntryRequest
            {
                ShowId = showId,
                SeasonId = seasonId,
                EpisodeNumber = episodeNumber,
                Backdate = watchedUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            };

            using var request = CreateRequest(HttpMethod.Post, "/show/reviews/add", token, body);
            var response = await Send(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidTokenException("Serializd rejected the stored access token");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to add diary entry for S{Season}E{Episode} of show {Show}: status {Status}",
                    seasonId,
                    episodeNumber,
                    showId,
                    response.StatusCode);
                return false;
            }

            return true;
        }

        public async Task<bool> UnlogEpisodeAsync(int showId, int seasonId, int episodeNumber, string token, CancellationToken cancellationToken = default)
        {
            var body = new LogEpisodesRequest
            {
                ShowId = showId,
                SeasonId = seasonId,
                EpisodeNumbers = new[] { episodeNumber }
            };

            using var request = CreateRequest(HttpMethod.Post, "/episode_log/remove", token, body);
            var response = await Send(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidTokenException("Serializd rejected the stored access token");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to remove log for S{Season}E{Episode} of show {Show}: status {Status}",
                    seasonId,
                    episodeNumber,
                    showId,
                    response.StatusCode);
                return false;
            }

            return true;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token = null, object? body = null)
        {
            var request = new HttpRequestMessage(method, BaseUrl + path);
            request.Headers.TryAddWithoutValidation("Origin", FrontPageUrl);
            request.Headers.TryAddWithoutValidation("Referer", FrontPageUrl);
            request.Headers.TryAddWithoutValidation("X-Requested-With", AppId);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (body != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);
            }

            return request;
        }

        private Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _httpClientFactory
                .CreateClient(NamedClient.Default)
                .SendAsync(request, cancellationToken);
        }
    }
}
