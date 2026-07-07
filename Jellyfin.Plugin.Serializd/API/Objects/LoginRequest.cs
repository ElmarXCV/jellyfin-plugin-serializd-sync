using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Serializd.API.Objects
{
    public class LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
