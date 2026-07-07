using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SerializdSync.API.Objects
{
    public class LogEpisodesRequest
    {
        [JsonPropertyName("episode_numbers")]
        public IReadOnlyList<int> EpisodeNumbers { get; set; } = new List<int>();

        [JsonPropertyName("season_id")]
        public int SeasonId { get; set; }

        [JsonPropertyName("show_id")]
        public int ShowId { get; set; }

        [JsonPropertyName("should_get_next_episode")]
        public bool ShouldGetNextEpisode { get; set; }
    }
}
