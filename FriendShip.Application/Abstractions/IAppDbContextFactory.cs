using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Abstractions
{
    public interface IAppDbContextFactory
    {
        IAppDbContext Create();
    }
}
