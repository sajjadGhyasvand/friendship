using FriendShip.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FriendShip.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private static readonly object Subject = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _hasher.HashPassword(Subject, password);
    }

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash) || password is null)
            return false;

        try
        {
            var result = _hasher.VerifyHashedPassword(
                Subject,
                hash,
                password);

            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // A malformed stored hash is not a valid password.
            return false;
        }
    }
}
