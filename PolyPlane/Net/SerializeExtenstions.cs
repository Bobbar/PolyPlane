using NetStack.Quantization;
using NetStack.Serialization;
using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    public static class SerializeExtenstions
    {
        public static void AddD2DPoint(this BitBuffer data, D2DPoint point)
        {
            var quant = BoundedRange.Quantize(point, World.WorldBounds);
            data.AddUInt(quant.x);
            data.AddUInt(quant.y);
        }

        public static D2DPoint ReadD2DPoint(this BitBuffer data)
        {
            var quant = new QuantizedVector2(data.ReadUInt(), data.ReadUInt());
            return BoundedRange.Dequantize(quant, World.WorldBounds);
        }


        public static void AddD2DColor(this BitBuffer data, D2DColor color)
        {
            data.AddUShort(HalfPrecision.Quantize(color.a));
            data.AddUShort(HalfPrecision.Quantize(color.r));
            data.AddUShort(HalfPrecision.Quantize(color.g));
            data.AddUShort(HalfPrecision.Quantize(color.b));
        }

        public static D2DColor ReadD2DColor(this BitBuffer data)
        {
            var color = new D2DColor();
            color.a = HalfPrecision.Dequantize(data.ReadUShort());
            color.r = HalfPrecision.Dequantize(data.ReadUShort());
            color.g = HalfPrecision.Dequantize(data.ReadUShort());
            color.b = HalfPrecision.Dequantize(data.ReadUShort());
            return color;
        }


        public static void AddFloat(this BitBuffer data, float value)
        {
            var quant = HalfPrecision.Quantize(value);
            data.AddUShort(quant);
        }

        public static float ReadFloat(this BitBuffer data)
        {
            return HalfPrecision.Dequantize(data.ReadUShort());
        }


        public static void AddGameID(this BitBuffer data, GameID id)
        {
            data.AddInt(id.PlayerID)
                .AddLong(id.ObjectID);
        }

        public static GameID ReadGameID(this BitBuffer data)
        {
            return new GameID(data.ReadInt(), data.ReadLong());
        }
    }
}
