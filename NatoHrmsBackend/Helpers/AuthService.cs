using System.Security.Cryptography;
using System.Text;

namespace NatoHrmsBackend.Helpers
{
	public static class AuthService
	{
		public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
		{
			using var hmac = new HMACSHA512();
			passwordSalt = hmac.Key;
			passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
		}

		public static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
		{
			using var hmac = new HMACSHA512(storedSalt);
			var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
			return computed.SequenceEqual(storedHash);
		}
	}
}
