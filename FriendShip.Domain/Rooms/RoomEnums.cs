using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Rooms
{
    public enum RoomStatus
    {
        Draft = 0,     // owner is still answering
        Open = 1,      // accepting friends
        Closed = 2,    // closed manually by owner
        Expired = 3,   // time ran out
        Cancelled = 4, // owner cancelled
        Paused = 5
    }

    public enum GameMode
    {
        Classic = 0,   // every correct answer = 100 points
        Speed = 1      // faster correct answers earn 80–100 points
    }

    public enum RevealMode
    {
        Immediate = 0,   // friend sees correct answers right after finishing
        AfterClose = 1   // revealed only after the room is closed/expired
    }
}
