using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.Net
{
    public enum PacketTypes
    {
        PlaneUpdate,
        MissileUpdate,
        Impact,
        NewPlayer,
        NewBullet,
        NewMissile,
        SetID,
        GetNextID,
        ChatMessage,
        GetOtherPlanes
            
    }
}
