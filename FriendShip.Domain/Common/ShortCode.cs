using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Common
{
    public static class ShortCode
    {
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        public static string Generate(int length)
        {
            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            return new string(chars);
        }

        public static bool IsValid(string? code, int length) =>
            code is not null && code.Length == length && code.All(c => Alphabet.Contains(c));

        public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
    }
}
