using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

[StructLayout(LayoutKind.Sequential)]
internal struct HeifDecodingOptionsPrefix
{
    public byte                       Version;
    public byte                       IgnoreTransformations;
    public nint                       StartProgress;
    public nint                       OnProgress;
    public nint                       EndProgress;
    public nint                       ProgressUserData;
    public byte                       ConvertHdrTo8Bit;
    public byte                       StrictDecoding;
    public nint                       DecoderId;
    public HeifColorConversionOptions HeifColorConversion;
    public nint                       CancelDecoding;
    public nint                       ColorConversionExtended;
    public int                        IgnoreSequenceEditlist;
}
