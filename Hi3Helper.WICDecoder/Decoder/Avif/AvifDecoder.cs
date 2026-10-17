using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Wic;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Avif;

[GeneratedComClass]
internal sealed partial class AvifDecoder : WicDecoderBase, IWICBitmapDecoder, IAnimationInfo, IAnimationTiming
{
    internal AvifDecoder(Guid decoderClsid)
    {
        DecoderClsid = decoderClsid;
    }

    protected override Guid DecoderClsid { get; }

    protected override Guid ContainerFormat => IdsConst.AvifContainer;

    protected override DecodedImage Decode(IStream stream)
    {
        return AvifBackend.Decode(stream);
    }
}
