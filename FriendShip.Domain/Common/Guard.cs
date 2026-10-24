using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FriendShip.Domain.Common
{
    public static partial class Guard
    {
        /// <summary>Normalizes Persian text (Arabic ي/ك → ی/ک, collapsed whitespace) and checks length.</summary>
        public static string Text(string? value, int min, int max, string code, string message)
        {
            var normalized = Normalize(value);
            if (normalized.Length < min || normalized.Length > max)
                throw new DomainException(code, message);
            return normalized;
        }

        public static void Against(bool condition, string code, string message)
        {
            if (condition) throw new DomainException(code, message);
        }

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var s = value.Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200F', ' ').Trim();
            return Spaces().Replace(s, " ");
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();
    }
}
