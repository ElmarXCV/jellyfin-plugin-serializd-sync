using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Serializd.API.Objects
{
    public class SeasonResponse
    {
        [JsonPropertyName("seasonId")]
        public int? SeasonId { get; set; }

        [JsonPropertyName("seasonNumber")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
