using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Wic;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;

namespace Hi3Helper.WICDecoder.Decoder.Webp;

[GeneratedComClass]
internal sealed partial class WebpDecoder : WicDecoderBase, IWICBitmapDecoder, IAnimationInfo, IAnimationTiming
{
    internal WebpDecoder(Guid decoderClsid)
    {
        DecoderClsid = decoderClsid;
    }

    protected override Guid DecoderClsid { get; }

    protected override Guid ContainerFormat => IdsConst.WebpContainer;

    protected override DecodedImage Decode(IStream stream)
    {
        return WebpBackend.Decode(stream);
    }
}
