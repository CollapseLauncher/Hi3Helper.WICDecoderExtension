using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlFrameHeader
{
    public uint         Duration;
    public uint         Timecode;
    public uint         NameLength;
    public int          IsLast;
    public JxlLayerInfo Layer;
}
