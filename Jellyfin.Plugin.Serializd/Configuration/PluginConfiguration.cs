using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Serializd.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            UserConfigs = Array.Empty<UserConfig>();
        }

        public UserConfig[] UserConfigs { get; set; }

        public UserConfig? GetByGuid(Guid id)
        {
            return UserConfigs.FirstOrDefault(c => c.Id == id);
        }

        public void SetCredentials(Guid id, string username, string token, string email, string password)
        {
            var config = GetOrCreate(id);
            config.Username = username;
            config.UserToken = token;
            config.Email = email;
            config.ProtectedPassword = SecretProtector.Protect(password);
        }

        public void SetToken(Guid id, string username, string token)
        {
            var config = GetOrCreate(id);
            config.Username = username;
            config.UserToken = token;
        }

        public void ClearCredentials(Guid id)
        {
            var config = GetByGuid(id);
            if (config != null)
            {
                config.UserToken = string.Empty;
                config.Email = string.Empty;
                config.ProtectedPassword = string.Empty;
            }
        }

        private UserConfig GetOrCreate(Guid id)
        {
            var config = GetByGuid(id);
            if (config == null)
            {
                config = new UserConfig { Id = id };
                var list = new List<UserConfig>(UserConfigs) { config };
                UserConfigs = list.ToArray();
            }

            return config;
        }
    }
}
