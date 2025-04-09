using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects
{
    [Flags]
    public enum GameObjectFlags
    {
        /// <summary>
        /// Will be added to the spatial grid for nearest neighbor lookups.
        /// </summary>
        SpatialGrid = 1,
        /// <summary>
        /// Object can be pushed by explosions and aerodynamic effects.
        /// </summary>
        Pushable = 2,
        /// <summary>
        /// Object will be clamped to the ground level with no bounce.
        /// </summary>
        ClampToGround = 4,
        /// <summary>
        /// Object will bounce off the ground.
        /// </summary>
        BounceOffGround = 8,
        /// <summary>
        /// Object will be put to sleep once its velocity is very close to zero.
        /// </summary>
        CanSleep = 16,

        NetObject = 32,
    }
}
