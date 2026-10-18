using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlLayerInfo
{
    public int          HaveCrop;
    public int          CropX;
    public int          CropY;
    public uint         Width;
    public uint         Height;
    public JxlBlendInfo Blend;
    public uint         SaveAsReference;
}
