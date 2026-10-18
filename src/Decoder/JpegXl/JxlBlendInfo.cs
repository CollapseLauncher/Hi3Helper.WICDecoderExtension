using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlBlendInfo
{
    public int  BlendMode;
    public uint Source;
    public uint AlphaChannel;
    public int  Clamp;
}
