using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using GroBuf;
using GroBuf.DataMembersExtracters;

namespace PolyPlane.Net
{
    public static class IO
    {
        public static Serializer _serializer = new Serializer(new FieldsExtractor(), options: GroBufOptions.WriteEmptyObjects);

        public static byte[] ObjectToByteArray(NetPacket obj)
        {
            var payloadBytes = _serializer.Serialize(obj.GetType(), obj);
            var payload = new PayloadPacket(obj.Type, payloadBytes);
            var bytes = _serializer.Serialize<PayloadPacket>(payload);
            return bytes;
        }

        public static Object ByteArrayToObject(byte[] arrBytes)
        {
            var payloadPacket = _serializer.Deserialize<PayloadPacket>(arrBytes);

            Object obj = null;

            switch (payloadPacket.Type)
            {
                case PacketTypes.PlaneUpdate:
                    obj = _serializer.Deserialize<PlaneListPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.MissileUpdate:
                    obj = _serializer.Deserialize<MissileListPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.Impact:
                    obj = _serializer.Deserialize<ImpactPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.NewPlayer:
                    obj = _serializer.Deserialize<PlanePacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.NewBullet:
                    obj = _serializer.Deserialize<BulletPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.NewMissile:
                    obj = _serializer.Deserialize<MissilePacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.NewDecoy:
                    obj = _serializer.Deserialize<DecoyPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.SetID or PacketTypes.GetNextID:
                    obj = _serializer.Deserialize<BasicPacket>(payloadPacket.Payload);

                    break;
                case PacketTypes.ChatMessage:

                    break;
                case PacketTypes.GetOtherPlanes:
                    obj = _serializer.Deserialize<PlaneListPacket>(payloadPacket.Payload);

                    break;

                case PacketTypes.ExpiredObjects:
                    obj = _serializer.Deserialize<BasicListPacket>(payloadPacket.Payload);

                    break;
            }

            return obj;
        }

        public static D2DPoint ToD2DPoint(this PointF point)
        {
            return new D2DPoint(point.X, point.Y);
        }

        public static D2DPoint ToD2DPoint(this NetPoint point)
        {
            return new D2DPoint(point.X, point.Y);
        }

        public static NetPoint ToPoint(this D2DPoint point)
        {
            return new NetPoint(point.X, point.Y);
        }



    }
}
