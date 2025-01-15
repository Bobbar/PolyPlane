using PolyPlane.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects.Interfaces
{
    public interface IPlayerScoredEvent
    {
        event EventHandler<PlayerScoredEventArgs> PlayerScoredEvent;
    }
}
