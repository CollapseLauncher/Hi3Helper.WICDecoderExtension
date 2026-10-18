using System.Runtime.InteropServices;

// ReSharper disable CommentTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

// Prefix through v8's ignore_sequence_editlist. Always allocate the full native
// struct, inspect its version, and change fields individually; never copy this
// shorter declaration over the allocation. C uint8_t fields are bytes, not bools.
[StructLayout(LayoutKind.Sequential)]
internal struct HeifColorConversionOptions
{
    public byte Version;
    public int  Downsampling;
    public int  Upsampling;
    public byte OnlyPreferred;
}