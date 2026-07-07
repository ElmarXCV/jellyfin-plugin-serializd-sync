using System;

namespace Jellyfin.Plugin.SerializdSync.API.Exceptions
{
    public class SerializdException : Exception
    {
        public SerializdException(string message)
            : base(message)
        {
        }
    }
}
