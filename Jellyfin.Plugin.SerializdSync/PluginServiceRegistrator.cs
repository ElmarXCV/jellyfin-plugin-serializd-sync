using Jellyfin.Plugin.SerializdSync.API;
using Jellyfin.Plugin.SerializdSync.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SerializdSync
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<SerializdApi>();
            serviceCollection.AddHostedService<PlaybackScrobbler>();
        }
    }
}
