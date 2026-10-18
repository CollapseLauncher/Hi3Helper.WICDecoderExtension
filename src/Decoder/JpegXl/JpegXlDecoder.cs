using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Wic;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[GeneratedComClass]
internal sealed partial class JpegXlDecoder : WicDecoderBase, IWICBitmapDecoder, IAnimationInfo, IAnimationTiming
{
    internal JpegXlDecoder(Guid decoderClsid)
    {
        DecoderClsid = decoderClsid;
    }

    protected override Guid DecoderClsid { get; }

    protected override Guid ContainerFormat => IdsConst.JpegXlContainer;

    protected override DecodedImage Decode(IStream stream)
    {
        return JpegXlBackend.Decode(stream);
    }
}
