namespace Jellyfin.Plugin.Serializd.API.Responses
{
    public class LoginResult
    {
        public bool Success { get; set; }

        public string? Username { get; set; }
    }
}
