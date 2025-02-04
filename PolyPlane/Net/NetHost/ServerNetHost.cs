using ENet;
using PolyPlane.Helpers;

namespace PolyPlane.Net.NetHost
{
    public class ServerNetHost : NetPlayHost
    {
        private Dictionary<uint, Peer> _peers = new Dictionary<uint, Peer>();
        private const int PEER_TIMEOUT = 10000;

        public ServerNetHost(ushort port, string ip) : base(port, ip)
        { }

        public override void DoStart()
        {
            base.DoStart();

            Host = new Host();
            Host.Create(Address, MAX_CLIENTS, MAX_CHANNELS);
        }

        public override void DoStop()
        {
            base.DoStop();

            foreach (var peer in _peers.Values)
                peer.DisconnectNow(0);
        }

        public override void HandleConnect(ref Event netEvent)
        {
            base.HandleConnect(ref netEvent);

            var peer = netEvent.Peer;

            peer.Timeout(PEER_TIMEOUT, PEER_TIMEOUT, PEER_TIMEOUT + 1000);

            _peers.Add(peer.ID, peer);

            var spawnPosition = Utilities.FindSafeSpawnPoint();
            var idPacket = new BasicPacket(PacketTypes.SetID, new GameObjects.GameID((int)peer.ID, 0), spawnPosition);
            idPacket.SendType = SendType.ToOnly;
            idPacket.PeerID = peer.ID;

            SendIDPacket(peer, idPacket);
        }

        public override void HandleDisconnect(ref Event netEvent)
        {
            base.HandleDisconnect(ref netEvent);

            var peer = netEvent.Peer;
            _peers.Remove(peer.ID);
        }

        public override void HandleTimeout(ref Event netEvent)
        {
            base.HandleTimeout(ref netEvent);

            var peer = netEvent.Peer;
            _peers.Remove(peer.ID);
        }

        public override void HandleReceive(NetPacket netPacket)
        {
            base.HandleReceive(netPacket);

            switch (netPacket.Type)
            {
                case PacketTypes.PlaneUpdate
                or PacketTypes.MissileUpdateList
                or PacketTypes.MissileUpdate
                or PacketTypes.ChatMessage
                or PacketTypes.NewBullet
                or PacketTypes.NewMissile
                or PacketTypes.NewDecoy
                or PacketTypes.Impact
                or PacketTypes.PlayerDisconnect
                or PacketTypes.PlayerReset
                or PacketTypes.PlayerEvent:

                    // Queue certain updates to re-broadcast ASAP.
                    PacketSendQueue.Enqueue(netPacket);
                    break;
            }
        }

        public override ulong PacketLoss()
        {
            return 0;
        }

        protected override void SendPacket(NetPacket netPacket)
        {
            var packet = CreatePacket(netPacket);
            var channel = GetChannel(netPacket);

            // Send the packet per the required send type.
            switch (netPacket.SendType)
            {
                case SendType.ToAll:

                    Host.Broadcast(channel, ref packet);
                 
                    break;

                case SendType.ToAllExcept:
                  
                    if (_peers.TryGetValue(netPacket.PeerID, out Peer excludePeer))
                        Host.Broadcast(channel, ref packet, excludePeer);
                    else
                        // Go ahead and broadcast to all if the peer ID is not found.
                        Host.Broadcast(channel, ref packet);

                    break;

                case SendType.ToOnly:

                    if (_peers.TryGetValue(netPacket.PeerID, out Peer includePeer))
                        includePeer.Send(channel, ref packet);

                    break;
            }
        }

        public override Peer? GetPeer(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
                return peer;

            return null;
        }

        public override void Disconnect(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out Peer peer))
            {
                peer.DisconnectNow(0);
                _peers.Remove((uint)playerID);
            }
        }

        public override double GetPlayerRTT(int playerID)
        {
            if (_peers.TryGetValue((uint)playerID, out var peer))
            {
                return peer.RoundTripTime;
            }

            return 0;
        }

        private void SendIDPacket(Peer peer, NetPacket packet)
        {
            var idPacket = CreatePacket(packet);
            var channel = GetChannel(packet);

            peer.Send(channel, ref idPacket);
        }
    }
}
