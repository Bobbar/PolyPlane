using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public interface ISkipFramesUpdate
    {
        /// <summary>
        /// How many frames must pass between updates.
        /// </summary>
        public long SkipFrames { get; set; }
        public long CurrentFrame { get; set; }
        public void Update(float dt, D2DSize viewport, float renderScale, bool skipFrames = false);

    }
}
