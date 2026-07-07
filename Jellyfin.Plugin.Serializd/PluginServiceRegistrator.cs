using Jellyfin.Plugin.Serializd.API;
using Jellyfin.Plugin.Serializd.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Serializd
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
