using System.Runtime.InteropServices;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct JxlBasicInfo
{
    public       int                HaveContainer;
    public       uint               Width;
    public       uint               Height;
    public       uint               BitsPerSample;
    public       uint               ExponentBitsPerSample;
    public       float              IntensityTarget;
    public       float              MinimumNits;
    public       int                RelativeToMaximumDisplay;
    public       float              LinearBelow;
    public       int                UsesOriginalProfile;
    public       int                HavePreview;
    public       int                HaveAnimation;
    public       int                Orientation;
    public       uint               ColorChannelCount;
    public       uint               ExtraChannelCount;
    public       uint               AlphaBits;
    public       uint               AlphaExponentBits;
    public       int                AlphaPremultiplied;
    public       JxlPreviewHeader   Preview;
    public       JxlAnimationHeader Animation;
    public       uint               IntrinsicWidth;
    public       uint               IntrinsicHeight;
    public fixed byte               Padding[100];
}