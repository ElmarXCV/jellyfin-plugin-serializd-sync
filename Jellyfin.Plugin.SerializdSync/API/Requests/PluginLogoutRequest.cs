using System;

namespace Jellyfin.Plugin.SerializdSync.API.Requests
{
    public class PluginLogoutRequest
    {
        public Guid UserId { get; set; }
    }
}
