using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct JxlPixelFormat
{
    public readonly uint          ChannelCount;
    public readonly JxlDataType   DataType;
    public readonly JxlEndianness Endianness;
    public readonly nuint         Alignment;

    internal JxlPixelFormat(uint          channelCount,
                            JxlDataType   dataType,
                            JxlEndianness endianness,
                            nuint         alignment)
    {
        ChannelCount = channelCount;
        DataType     = dataType;
        Endianness   = endianness;
        Alignment    = alignment;
    }
}