﻿namespace PolyPlane.Net
{
    public enum SendType
    {
        /// <summary>
        /// Broadcast to all connected clients.
        /// </summary>
        ToAll,
        /// <summary>
        /// Broadcast to all clients except the one the packet originated from.
        /// </summary>
        ToAllExcept,
        /// <summary>
        /// Send only to the peer specified in the packet.
        /// </summary>
        ToOnly
    }
}
