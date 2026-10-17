using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.Heif;

[StructLayout(LayoutKind.Sequential)]
internal struct HeifError
{
    public int  Code;
    public int  Subcode;
    public nint Message;
}
