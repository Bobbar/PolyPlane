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
                bitBuffer = new BitBuffer(256);

            return bitBuffer;
        }
    }

    public static class Serialization
    {
        public const bool EnableCompression = true;

        public static byte[] ObjectToByteArray(NetPacket obj)
        {
            var data = BufferPool.GetBitBuffer();

            obj.Serialize(data);

            var bytes = new byte[data.Length];
            data.ToArray(bytes);
            data.Clear();

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
                case PacketTypes.PlaneListUpdate:
                    obj = new PlaneListPacket(data);
                    break;

                case PacketTypes.PlaneUpdate:
                    obj = new PlanePacket(data);
                    break;

                case PacketTypes.GetOtherPlanes:
                    obj = new PlayerListPacket(data);
                    break;

                case PacketTypes.MissileUpdateList or PacketTypes.MissileList:
                    obj = new MissileListPacket(data);
                    break;

                case PacketTypes.Impact:
                    obj = new ImpactPacket(data);
                    break;

                case PacketTypes.NewPlayer:
                    obj = new NewPlayerPacket(data);
                    break;

                case PacketTypes.NewMissile or PacketTypes.MissileUpdate:
                    obj = new MissilePacket(data);
                    break;

                case PacketTypes.NewDecoy or PacketTypes.NewBullet:
                    obj = new GameObjectPacket(data);
                    break;

                case PacketTypes.SetID or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset or PacketTypes.KickPlayer:
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

                case PacketTypes.BulletList or PacketTypes.DecoyList:
                    obj = new GameObjectListPacket(data);
                    break;

                case PacketTypes.PlayerEvent:
                    obj = new PlayerEventPacket(data);
                    break;

                case PacketTypes.ScoreEvent:
                    obj = new PlayerScoredPacket(data);
                    break;

                case PacketTypes.PlaneStatusList:
                    obj = new PlaneStatusListPacket(data);
                    break;

                case PacketTypes.PlaneStatus:
                    obj = new PlaneStatusPacket(data);
                    break;

            }

            data.Clear();

            if (obj == null)
                throw new Exception($"Failed to parse packet of type: {type}");

            return obj;
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
