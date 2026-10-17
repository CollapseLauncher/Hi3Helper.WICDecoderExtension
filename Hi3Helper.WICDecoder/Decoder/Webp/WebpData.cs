using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.Webp;


[StructLayout(LayoutKind.Sequential)]
internal struct WebpData
{
    public nint  Bytes;
    public nuint Size;
}
