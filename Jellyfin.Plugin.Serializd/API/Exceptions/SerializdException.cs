using System;

namespace Jellyfin.Plugin.Serializd.API.Exceptions
{
    public class SerializdException : Exception
    {
        public SerializdException(string message)
            : base(message)
        {
        }
    }
}
