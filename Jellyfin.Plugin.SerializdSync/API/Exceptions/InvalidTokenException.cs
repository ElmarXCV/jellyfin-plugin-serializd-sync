namespace Jellyfin.Plugin.SerializdSync.API.Exceptions
{
    public class InvalidTokenException : SerializdException
    {
        public InvalidTokenException(string message)
            : base(message)
        {
        }
    }
}
