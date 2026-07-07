using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.Serializd.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Serializd
{
    public class SerializdPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public SerializdPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            SecretProtector.Initialize(Path.Combine(applicationPaths.DataPath, "serializd"));
        }

        public static SerializdPlugin? Instance { get; private set; }

        public override Guid Id => new Guid("4C5E4DAF-FD7C-4F1D-9B47-C011D9070D94");

        public override string Name => "Serializd";

        public override string Description => "Scrobble your watched TV shows to Serializd as you play them in Jellyfin.";

        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            };
        }
    }
}
