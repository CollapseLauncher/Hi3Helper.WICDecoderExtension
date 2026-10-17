using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Wic;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

[GeneratedComClass]
internal sealed partial class HeifDecoder : WicDecoderBase, IWICBitmapDecoder, IAnimationInfo, IAnimationTiming
{
    internal HeifDecoder(Guid decoderClsid)
    {
        DecoderClsid = decoderClsid;
    }

    protected override Guid DecoderClsid { get; }

    protected override Guid ContainerFormat => IdsConst.HeifContainer;

    protected override DecodedImage Decode(IStream stream)
    {
        return HeifBackend.Decode(stream, HeifContainerKind.Heif);
    }
}
