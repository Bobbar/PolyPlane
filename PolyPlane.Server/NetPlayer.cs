using PolyPlane.GameObjects;

namespace PolyPlane.Server
{
    public sealed class NetPlayer
    {
        public GameID ID;
        public string Name;
        public string IP;
        public string Latency;
        public string PacketLoss;
        public int Score;

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
            return $"[{ID.PlayerID}]   Name: {Name}  IP: {IP}  Ping: {Latency}  P-Loss: {PacketLoss}  Score: {Score}";
        }
    }
}
