using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Common
{
    public sealed record Caller(Guid? UserId, Guid VisitorId, string Ip, bool IsAdmin = false)
    {
        public bool IsAuthenticated => UserId.HasValue;
    }
}
