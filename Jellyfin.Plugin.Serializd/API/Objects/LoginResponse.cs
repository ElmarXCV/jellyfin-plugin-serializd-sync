using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Serializd.API.Objects
{
    public class LoginResponse
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}
