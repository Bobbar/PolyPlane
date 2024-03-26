using ENet;
using System.Diagnostics;

namespace PolyPlane.Net
{
    public class ClientNetHost : NetPlayHost
    {
        public Peer Peer;

        public ClientNetHost(ushort port, string ip) : base(port, ip)
        {
        }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create();

            Peer = Host.Connect(Address, MAX_CHANNELS);
        }

        public override void HandleConnect(Event netEvent)
        {
            base.HandleConnect(netEvent);

            RequestOtherPlanes();
        }

        public override void HandleDisconnect(Event netEvent)
        {
            base.HandleDisconnect(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override void HandleTimeout(Event netEvent)
        {
            base.HandleTimeout(netEvent);

            SendPlayerDisconnectPacket(netEvent.Peer.ID);
        }

        public override void HandleReceive(Event netEvent)
        {
            base.HandleReceive(netEvent);

            ParsePacket(netEvent.Packet);
        }

        public override void SendPacket(NetPacket packet)
        {
            base.SendPacket(packet);

            Packet netPacket = default(Packet);
            var data = IO.ObjectToByteArray(packet);

            netPacket.Create(data, PacketFlags.Reliable);
            //packet.Create(data, PacketFlags.Instant);
            Peer.Send(CHANNEL_ID, ref netPacket);
        }

        private void ParsePacket(Packet packet)
        {
            var buffer = new byte[packet.Length];
            packet.CopyTo(buffer);
            var packetObj = IO.ByteArrayToObject(buffer) as NetPacket;

            PacketReceiveQueue.Enqueue(packetObj);
        }

        private void RequestOtherPlanes()
        {
            var netPacket = new BasicPacket(PacketTypes.GetOtherPlanes, new GameObjects.GameID(-1, (int)Peer.ID));
            SendPacket(netPacket);
        }

        public void SendPlayerDisconnectPacket(uint playerID)
        {
            var packet = new BasicPacket(PacketTypes.PlayerDisconnect, new GameObjects.GameID(playerID));

            SendPacket(packet);

            Host.Flush();
        }



    }
}
