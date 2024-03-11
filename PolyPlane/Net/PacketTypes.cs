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
        NewPlayer,
        NewBullet,
        SetID,
        GetNextID,
        ChatMessage,
        GetOtherPlanes
            
    }
}
