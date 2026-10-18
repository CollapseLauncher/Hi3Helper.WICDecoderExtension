using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Decoder.Avif;
using Hi3Helper.WICDecoder.Decoder.Heif;
using Hi3Helper.WICDecoder.Decoder.JpegXl;
using Hi3Helper.WICDecoder.Decoder.Webp;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using System;
using System.IO;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder;

// Registration metadata only. Decoder implementation and COM identity live in the
// separate WebpDecoder, AvifDecoder, and HeifDecoder types.
public sealed class DecoderDescriptor
{
    public readonly string                        Name;
    public readonly Guid                          Decoder;
    public readonly Guid                          Container;
    public readonly string                        MimeTypes;
    public readonly string                        FileExtensions;
    public readonly bool                          Animation;
    public readonly DecoderPattern[]              Patterns;
    public readonly Func<Guid, IWICBitmapDecoder> CreateDecoder;

    internal DecoderDescriptor(string                        name,
                               Guid                          decoder,
                               Guid                          container,
                               string                        mimeTypes,
                               string                        fileExtensions,
                               bool                          animation,
                               DecoderPattern[]              patterns,
                               Func<Guid, IWICBitmapDecoder> createDecoder)
    {
        Name           = name;
        Decoder        = decoder;
        Container      = container;
        MimeTypes      = mimeTypes;
        FileExtensions = fileExtensions;
        Animation      = animation;
        Patterns       = patterns;
        CreateDecoder  = createDecoder;
    }

    public static DecoderDescriptor Detect(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return Webp;
        }

        if (extension.Equals(".avif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".clavif", StringComparison.OrdinalIgnoreCase))
        {
            return Avif;
        }

        if (extension.Equals(".heif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".heic", StringComparison.OrdinalIgnoreCase))
        {
            return Heif;
        }

        throw new NotSupportedException(
            $"The '{extension}' image extension is not supported.");
    }

    public static readonly DecoderDescriptor Webp = new(
        "WebP",
        IdsConst.WebpDecoder,
        IdsConst.WebpContainer,
        "image/webp",
        ".webp",
        true,
        [
            new DecoderPattern(
                0,
                [.. "RIFF\0\0\0\0WEBP"u8],
                [0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF])
        ],
        static decoder => new WebpDecoder(decoder));

    // Private sample GUIDs, not a claim to Windows' HEIF container identity.
    public static readonly DecoderDescriptor Avif = new(
        "AVIF",
        IdsConst.AvifDecoder,
        IdsConst.AvifContainer,
        "image/avif",
        ".avif,.clavif",
        true,
        [
            new DecoderPattern(4, [.. "ftypavif"u8], [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]),
            new DecoderPattern(4, [.. "ftypavis"u8], [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF])
        ],
        static decoder => new AvifDecoder(decoder));

    // HEIF is the container family; HEIC is its common HEVC-based profile.
    // The generic mif1/msf1 patterns are accepted here, while an explicit
    // avif/avis brand is rejected by HeifBackend and handled by AvifDecoder.
    public static readonly DecoderDescriptor Heif = new(
        "HEIF/HEIC",
        IdsConst.HeifDecoder,
        IdsConst.HeifContainer,
        "image/heif,image/heic,image/heif-sequence,image/heic-sequence",
        ".heif,.heic",
        true,
        [
            CreateHeifPattern("heic"),
            CreateHeifPattern("heix"),
            CreateHeifPattern("heim"),
            CreateHeifPattern("heis"),
            CreateHeifPattern("hevc"),
            CreateHeifPattern("hevx"),
            CreateHeifPattern("hevm"),
            CreateHeifPattern("hevs"),
            CreateHeifPattern("mif1"),
            CreateHeifPattern("msf1")
        ],
        static decoder => new HeifDecoder(decoder));

    private static DecoderPattern CreateHeifPattern(string brand)
    {
        byte[] pattern = new byte[8];
        "ftyp"u8.CopyTo(pattern);
        System.Text.Encoding.ASCII.GetBytes(brand, pattern.AsSpan(4));

        return new DecoderPattern(
                                  4,
                                  pattern,
                                  [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
    }

    public static readonly DecoderDescriptor JpegXl = new(
        "JPEG XL",
        IdsConst.JpegXlDecoder,
        IdsConst.JpegXlContainer,
        "image/jxl",
        ".jxl",
        true,
        [
            new DecoderPattern(0, [0xFF, 0x0A], [0xFF, 0xFF]),
            new DecoderPattern(
                0,
                [0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A],
                [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF])
        ],
        static decoder => new JpegXlDecoder(decoder));

    public override int GetHashCode() => Decoder.GetHashCode();
}

public sealed record DecoderPattern(uint Position, byte[] Pattern, byte[] Mask);
