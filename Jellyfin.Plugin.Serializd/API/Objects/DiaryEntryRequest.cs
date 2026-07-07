using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Serializd.API.Objects
{
    public class DiaryEntryRequest
    {
        [JsonPropertyName("show_id")]
        public int ShowId { get; set; }

        [JsonPropertyName("season_id")]
        public int SeasonId { get; set; }

        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("backdate")]
        public string Backdate { get; set; } = string.Empty;

        [JsonPropertyName("review_text")]
        public string ReviewText { get; set; } = string.Empty;

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("contains_spoiler")]
        public bool ContainsSpoiler { get; set; }

        [JsonPropertyName("is_log")]
        public bool IsLog { get; set; } = true;

        [JsonPropertyName("is_rewatch")]
        public bool IsRewatch { get; set; }

        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();

        [JsonPropertyName("allows_comments")]
        public bool AllowsComments { get; set; } = true;

        [JsonPropertyName("like")]
        public bool Like { get; set; }
    }
}
