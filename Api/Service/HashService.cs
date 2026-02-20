using System.Security.Cryptography;
using System.Text;

namespace Api.Service
{
    public class HashService
    {
        public static string GenerateSalt()
        {
            return Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(16));
        }

        public static string Hash(string unhasheddata, string salt)
        {
            var data = Encoding.UTF8.GetBytes(unhasheddata + salt);
            var hash = SHA256.HashData(data);
            return Convert.ToBase64String(hash);
        }
    }
}
