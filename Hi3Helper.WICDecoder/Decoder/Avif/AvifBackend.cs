using Hi3Helper.WICDecoder.Decoder.Heif;
using Hi3Helper.Win32.Native.Interfaces;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Avif;

internal static class AvifBackend
{
    public static DecodedImage Decode(IStream stream)
    {
        return HeifBackend.Decode(stream, HeifContainerKind.Avif);
    }
}
