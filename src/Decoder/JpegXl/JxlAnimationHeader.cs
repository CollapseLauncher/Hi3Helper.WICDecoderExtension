using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlAnimationHeader
{
    public uint TicksPerSecondNumerator;
    public uint TicksPerSecondDenominator;
    public uint LoopCount;
    public int  HaveTimecodes;
}
