using NetStack.Buffers;
using NetStack.Serialization;
using System.IO.Compression;

namespace PolyPlane.Net
{
    static class BufferPool
    {
        [ThreadStatic]
        private static BitBuffer bitBuffer;

        public static BitBuffer GetBitBuffer()
        {
            if (bitBuffer == null)
                bitBuffer = new BitBuffer(1024);

            return bitBuffer;
        }
    }

    public static class Serialization
    {
        public static bool EnableCompression = true;

        private static ArrayPool<byte> _buffers = ArrayPool<byte>.Create(1024, 50);

        public static byte[] ObjectToByteArray(NetPacket obj)
        {
            var data = BufferPool.GetBitBuffer();

            SerializeType(obj, data);

            var bytes = _buffers.Rent(data.Length);
            data.ToArray(bytes);
            data.Clear();
            _buffers.Return(bytes);

            if (EnableCompression)
                bytes = Compress(bytes);

            return bytes;
        }

     
        public static object ByteArrayToObject(byte[] arrBytes)
        {
            var data = BufferPool.GetBitBuffer();

            if (EnableCompression)
                arrBytes = Decompress(arrBytes);

            data.FromArray(arrBytes, arrBytes.Length);

            var type = (PacketTypes)data.PeekByte();

            object obj = null;

            switch (type)
            {
                case PacketTypes.PlaneUpdate:
                    obj = new PlaneListPacket(data);
                    break;

                case PacketTypes.GetOtherPlanes:
                    obj = new PlayerListPacket(data);
                    break;

                case PacketTypes.MissileUpdate:
                    obj = new MissileListPacket(data);
                    break;

                case PacketTypes.Impact:
                    obj = new ImpactPacket(data);
                    break;

                case PacketTypes.NewPlayer:
                    obj = new NewPlayerPacket(data);
                    break;

                case PacketTypes.NewMissile:
                    obj = new MissilePacket(data);
                    break;

                case PacketTypes.NewDecoy or PacketTypes.NewBullet:
                    obj = new GameObjectPacket(data);
                    break;

                case PacketTypes.SetID or PacketTypes.GetNextID or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset or PacketTypes.KickPlayer:
                    obj = new BasicPacket(data);
                    break;

                case PacketTypes.ChatMessage:
                    obj = new ChatPacket(data);
                    break;

                case PacketTypes.ExpiredObjects:
                    obj = new BasicListPacket(data);
                    break;

                case PacketTypes.ServerSync:
                    obj = new SyncPacket(data);
                    break;

                case PacketTypes.Discovery:
                    obj = new DiscoveryPacket(data);
                    break;

                case PacketTypes.ImpactList:
                    obj = new ImpactListPacket(data);
                    break;
            }

            data.Clear();

            return obj;
        }


        private static void SerializeType(NetPacket packet, BitBuffer data)
        {
            var type = packet.Type;

            switch (type)
            {
                case PacketTypes.PlaneUpdate:
                    var planeUpdate = packet as PlaneListPacket;
                    planeUpdate.Serialize(data);
                    break;

                case PacketTypes.GetOtherPlanes: // TODO: Maybe split this into Request & Sent.

                    // Clients sent a basic packet to request.
                    // Server sends a list packet as a response.
                    if (packet is PlayerListPacket playerList)
                        playerList.Serialize(data);
                    else if (packet is BasicPacket basicPack)
                        basicPack.Serialize(data);

                    break;

                case PacketTypes.MissileUpdate:
                    var missileListPacket = packet as MissileListPacket;
                    missileListPacket.Serialize(data);
                    break;

                case PacketTypes.Impact:
                    var impactPacket = packet as ImpactPacket;
                    impactPacket.Serialize(data);
                    break;

                case PacketTypes.NewPlayer:
                    var newPlayerPacket = packet as NewPlayerPacket;
                    newPlayerPacket.Serialize(data);
                    break;

                case PacketTypes.NewMissile:
                    var missilePacket = packet as MissilePacket;
                    missilePacket.Serialize(data);
                    break;

                case PacketTypes.NewDecoy or PacketTypes.NewBullet:
                    var gameObjectPacket = packet as GameObjectPacket;
                    gameObjectPacket.Serialize(data);
                    break;

                case PacketTypes.SetID or PacketTypes.GetNextID or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset or PacketTypes.KickPlayer:
                    var basicPacket = packet as BasicPacket;
                    basicPacket.Serialize(data);
                    break;

                case PacketTypes.ChatMessage:
                    var chatPacket = packet as ChatPacket;
                    chatPacket.Serialize(data);
                    break;

                case PacketTypes.ExpiredObjects:
                    var basicListPacket = packet as BasicListPacket;
                    basicListPacket.Serialize(data);
                    break;

                case PacketTypes.ServerSync:
                    var syncPacket = packet as SyncPacket;
                    syncPacket.Serialize(data);
                    break;

                case PacketTypes.Discovery:
                    var discoveryPacket = packet as DiscoveryPacket;
                    discoveryPacket.Serialize(data);
                    break;

                case PacketTypes.ImpactList:
                    var impactListPacket = packet as ImpactListPacket;
                    impactListPacket.Serialize(data);
                    break;
            }
        }


        private static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.SmallestSize))
            {
                dstream.Write(data, 0, data.Length);
            }

            output.Dispose();
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }

            input.Dispose();
            return output.ToArray();
        }
    }
}
