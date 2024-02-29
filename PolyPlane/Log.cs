﻿using System.Diagnostics;

namespace PolyPlane
{
    public static class Log
    {
        public static bool Enabled = false;
        public static void Msg(string message)
        {
            if (Enabled)
                Debug.WriteLine(message);
        }
    }
}
