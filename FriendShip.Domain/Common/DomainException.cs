using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Common
{
    public sealed class DomainException : Exception
    {
        public string Code { get; }

        public DomainException(string code, string message) : base(message) => Code = code;
    }
}
