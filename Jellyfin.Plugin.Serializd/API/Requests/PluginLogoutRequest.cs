using System;

namespace Jellyfin.Plugin.Serializd.API.Requests
{
    public class PluginLogoutRequest
    {
        public Guid UserId { get; set; }
    }
}
