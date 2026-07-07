using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;

namespace Jellyfin.Plugin.SerializdSync.Configuration
{
    public static class SecretProtector
    {
        private static IDataProtector? _protector;

        public static void Initialize(string keysDirectory)
        {
            var dir = new DirectoryInfo(keysDirectory);
            if (!dir.Exists)
            {
                dir.Create();
            }

            _protector = DataProtectionProvider
                .Create(dir)
                .CreateProtector("Jellyfin.Plugin.SerializdSync.Credentials");
        }

        public static string Protect(string plaintext)
        {
            if (_protector == null || string.IsNullOrEmpty(plaintext))
            {
                return string.Empty;
            }

            return _protector.Protect(plaintext);
        }

        public static string Unprotect(string ciphertext)
        {
            if (_protector == null || string.IsNullOrEmpty(ciphertext))
            {
                return string.Empty;
            }

            try
            {
                return _protector.Unprotect(ciphertext);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
