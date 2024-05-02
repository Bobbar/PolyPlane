using NetStack.Quantization;
using NetStack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using unvell.D2DLib;

namespace PolyPlane.Net
{
    public static class SerializeExtenstions
    {
        public static void Serialize(this D2DPoint vec, BitBuffer data)
        {
            var quant = BoundedRange.Quantize(vec, World.WorldBounds);
            data.AddUInt(quant.x);
            data.AddUInt(quant.y);
        }

        public static void Deserialize(this ref D2DPoint vec, BitBuffer data)
        {
            var quant = new QuantizedVector2(data.ReadUInt(), data.ReadUInt());
            vec = BoundedRange.Dequantize(quant, World.WorldBounds);

        }

        public static void Serialize(this D2DColor color, BitBuffer data)
        {
            data.AddUShort(HalfPrecision.Quantize(color.a));
            data.AddUShort(HalfPrecision.Quantize(color.r));
            data.AddUShort(HalfPrecision.Quantize(color.g));
            data.AddUShort(HalfPrecision.Quantize(color.b));
        }

        public static void Deserialize(this ref D2DColor color, BitBuffer data)
        {
            color.a = HalfPrecision.Dequantize(data.ReadUShort());
            color.r = HalfPrecision.Dequantize(data.ReadUShort());
            color.g = HalfPrecision.Dequantize(data.ReadUShort());
            color.b = HalfPrecision.Dequantize(data.ReadUShort());
        }

        public static void Serialize(this float value, BitBuffer data)
        {
            var quant = HalfPrecision.Quantize(value);
            data.AddUShort(quant);
        }

        public static void Deserialize(this ref float value, BitBuffer data)
        {
            value = HalfPrecision.Dequantize(data.ReadUShort());
        }
    }
}
