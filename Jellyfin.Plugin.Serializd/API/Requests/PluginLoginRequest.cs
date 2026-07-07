using System;

namespace Jellyfin.Plugin.Serializd.API.Requests
{
    public class PluginLoginRequest
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
