using FriendShip.Domain.Achievements;
using FriendShip.Domain.Moderation;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Questions;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Abstractions
{
   

    public interface IRateLimiter
    {
        /// <summary>Returns false when the key exceeded <paramref name="limit"/> hits within <paramref name="window"/>.</summary>
        bool TryAcquire(string key, int limit, TimeSpan window);
    }
}
