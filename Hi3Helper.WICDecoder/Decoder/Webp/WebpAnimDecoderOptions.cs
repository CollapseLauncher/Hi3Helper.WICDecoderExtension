using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.Webp;


[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WebpAnimDecoderOptions
{
    public       int  ColorMode;
    public       int  UseThreads;
    public fixed uint Padding[7];
}
