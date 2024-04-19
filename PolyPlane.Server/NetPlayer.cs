using PolyPlane.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.Server
{
    public class NetPlayer
    {
        public GameID ID;
        public string Name;
        public string IP;
        public string Latency;

        public NetPlayer()
        {

        }

        public NetPlayer(GameID id, string name)
        {
            ID = id;
            Name = name;
        }

        public NetPlayer(GameID id, string name, string ip, string latency)
        {
            ID = id;
            Name = name;
        }


        public override string ToString()
        {
            return $"[{ID.PlayerID}]    {Name}   {IP}    {Latency}";
        }
    }
}
