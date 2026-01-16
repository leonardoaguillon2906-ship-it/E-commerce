using Microsoft.AspNetCore.Identity;

namespace EcommerceApp.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<string> _hasher;

        public PasswordService()
        {
            _hasher = new PasswordHasher<string>();
        }

        // HASH DE PASSWORD
        public string Hash(string password)
        {
            return _hasher.HashPassword(null, password);
        }

        // VALIDACIÓN
        public bool Verify(string hash, string password)
        {
            var result = _hasher.VerifyHashedPassword(
                null,
                hash,
                password
            );

            return result == PasswordVerificationResult.Success;
        }
    }
}
