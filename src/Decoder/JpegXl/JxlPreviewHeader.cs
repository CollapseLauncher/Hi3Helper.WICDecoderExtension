using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlPreviewHeader
{
    public uint Width;
    public uint Height;
}