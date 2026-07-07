namespace Jellyfin.Plugin.Serializd.API.Exceptions
{
    public class InvalidTokenException : SerializdException
    {
        public InvalidTokenException(string message)
            : base(message)
        {
        }
    }
}
