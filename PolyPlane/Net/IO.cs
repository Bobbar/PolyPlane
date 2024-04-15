using GroBuf;
using GroBuf.DataMembersExtracters;
using System.IO.Compression;

namespace PolyPlane.Net
{
    public static class IO
    {
        public static bool EnableCompression = true;

        private static Serializer _serializer = new Serializer(new FieldsExtractor(), options: GroBufOptions.WriteEmptyObjects);

        public static byte[] ObjectToByteArray(NetPacket obj)
        {
            var payloadBytes = _serializer.Serialize(obj.GetType(), obj);
            var payload = new PayloadPacket(obj.Type, payloadBytes);
            var bytes = _serializer.Serialize<PayloadPacket>(payload);

            if (EnableCompression)
                bytes = Compress(bytes);

            return bytes;
        }

        public static object ByteArrayToObject(byte[] arrBytes)
        {
            if (EnableCompression)
                arrBytes = Decompress(arrBytes);

            var payloadPacket = _serializer.Deserialize<PayloadPacket>(arrBytes);
            var payloadBytes = payloadPacket.Payload;

            object obj = null;

            switch (payloadPacket.Type)
            {
                case PacketTypes.PlaneUpdate:
                    obj = _serializer.Deserialize<PlaneListPacket>(payloadBytes);
                    break;

                case PacketTypes.GetOtherPlanes:
                    obj = _serializer.Deserialize<PlayerListPacket>(payloadBytes);
                    break;

                case PacketTypes.MissileUpdate:
                    obj = _serializer.Deserialize<MissileListPacket>(payloadBytes);
                    break;

                case PacketTypes.Impact:
                    obj = _serializer.Deserialize<ImpactPacket>(payloadBytes);
                    break;

                case PacketTypes.NewPlayer:
                    obj = _serializer.Deserialize<NewPlayerPacket>(payloadBytes);
                    break;

                case PacketTypes.NewBullet:
                    obj = _serializer.Deserialize<BulletPacket>(payloadBytes);
                    break;

                case PacketTypes.NewMissile:
                    obj = _serializer.Deserialize<MissilePacket>(payloadBytes);
                    break;

                case PacketTypes.NewDecoy:
                    obj = _serializer.Deserialize<DecoyPacket>(payloadBytes);
                    break;

                case PacketTypes.SetID or PacketTypes.GetNextID or PacketTypes.PlayerDisconnect or PacketTypes.PlayerReset:
                    obj = _serializer.Deserialize<BasicPacket>(payloadBytes);
                    break;

                case PacketTypes.ChatMessage:
                    break;

                case PacketTypes.ExpiredObjects:
                    obj = _serializer.Deserialize<BasicListPacket>(payloadBytes);
                    break;

                case PacketTypes.ServerSync:
                    obj = _serializer.Deserialize<SyncPacket>(payloadBytes);
                    break;

                case PacketTypes.Discovery:
                    obj = _serializer.Deserialize<DiscoveryPacket>(payloadBytes);
                    break;
            }

            return obj;
        }

        private static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Fastest))
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

        public static D2DPoint ToD2DPoint(this PointF point)
        {
            return new D2DPoint(point.X, point.Y);
        }

        public static D2DPoint ToD2DPoint(this NetPoint point)
        {
            return new D2DPoint(point.X, point.Y);
        }

        public static NetPoint ToNetPoint(this D2DPoint point)
        {
            return new NetPoint(point.X, point.Y);
        }



    }
}
