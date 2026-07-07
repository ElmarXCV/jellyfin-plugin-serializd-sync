using System;

namespace Jellyfin.Plugin.SerializdSync.Configuration
{
    public class UserConfig
    {
        public UserConfig()
        {
            ScrobbleShows = true;
            LogToDiary = true;
            ScrobblePercentage = 70;
            MinLength = 5;
            ScrobbleTimeout = 30;
            UserToken = string.Empty;
            Username = string.Empty;
            Email = string.Empty;
            ProtectedPassword = string.Empty;
        }

        public Guid Id { get; set; }

        public string UserToken { get; set; }

        public string Username { get; set; }

        public string Email { get; set; }

        public string ProtectedPassword { get; set; }

        public bool ScrobbleShows { get; set; }

        public bool LogToDiary { get; set; }

        public int ScrobblePercentage { get; set; }

        public int MinLength { get; set; }

        public int ScrobbleTimeout { get; set; }
    }
}
