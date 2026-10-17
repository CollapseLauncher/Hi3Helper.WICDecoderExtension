using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.Win32.Native.Interfaces;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

internal static unsafe partial class HeifBackend
{
    private const uint FileTypeBox = 0x66747970; // ftyp
    private const uint MovieBox    = 0x6D6F6F76; // moov
    private const uint AvifBrand   = 0x61766966; // avif
    private const uint AvisBrand   = 0x61766973; // avis
    private const uint HeicBrand   = 0x68656963; // heic
    private const uint HeixBrand   = 0x68656978; // heix
    private const uint HeimBrand   = 0x6865696D; // heim
    private const uint HeisBrand   = 0x68656973; // heis
    private const uint HevcBrand   = 0x68657663; // hevc
    private const uint HevxBrand   = 0x68657678; // hevx
    private const uint HevmBrand   = 0x6865766D; // hevm
    private const uint HevsBrand   = 0x68657673; // hevs
    private const uint Mif1Brand   = 0x6D696631; // mif1
    private const uint Msf1Brand   = 0x6D736631; // msf1

    private const int  RgbColorSpace      = 1;
    private const int  ChromaRgba         = 11;
    private const int  ChannelInterleaved = 10;
    private const long MaxPixels          = 16 * 1024 * 1024;

    public static DecodedImage Decode(IStream stream, HeifContainerKind containerKind)
    {
        using EncodedInput encoded    = EncodedInput.Read(stream);
        bool               isSequence = ValidateContainer(encoded.Span, containerKind);

        InitializeLibheif();

        try
        {
            return DecodeContainer(encoded, isSequence);
        }
        finally
        {
            HeifInterop.heif_deinit();
        }
    }

    private static void InitializeLibheif()
    {
        Check(HeifInterop.heif_init(0));
    }

    private static DecodedImage DecodeContainer(EncodedInput encoded, bool isSequence)
    {
        nint context = HeifInterop.heif_context_alloc();
        if (context == 0)
        {
            throw new OutOfMemoryException();
        }

        try
        {
            ReadContainer(context, encoded);

            return isSequence
                ? DecodeSequence(context, encoded.Span)
                : DecodeStillImage(context);
        }
        finally
        {
            HeifInterop.heif_context_free(context);
        }
    }

    private static void ReadContainer(nint context, EncodedInput encoded)
    {
        Check(HeifInterop.heif_context_read_from_memory_without_copy(context, encoded.Buffer, encoded.Length, 0));
    }

    private static DecodedImage DecodeStillImage(nint context)
    {
        ValidateTopLevelImageCount(context);

        nint handle = 0;
        nint image  = 0;

        try
        {
            Check(HeifInterop.heif_context_get_primary_image_handle(context, out handle));
            ValidateImageHandle(handle);

            bool hasAlpha = HeifInterop.heif_image_handle_has_alpha_channel(handle) != 0;

            // RGB=1, interleaved_RGBA=11. Default options apply container transforms.
            // Requesting 8-bit RGBA reduces higher bit depths; this is not HDR output.
            Check(HeifInterop.heif_decode_image(handle, out image, RgbColorSpace, ChromaRgba, 0));
            ImageFrame frame = CopyFrame(image, hasAlpha);
            return new DecodedImage([frame], false, 0, 0);
        }
        finally
        {
            if (image != 0)
            {
                HeifInterop.heif_image_release(image);
            }

            if (handle != 0)
            {
                HeifInterop.heif_image_handle_release(handle);
            }
        }
    }

    private static void ValidateTopLevelImageCount(nint context)
    {
        int imageCount = HeifInterop.heif_context_get_number_of_top_level_images(context);
        if (imageCount is <= 0 or > 4096)
        {
            throw new COMException("Invalid HEIF top-level image count.", HResultConst.BadImage);
        }

        // HEIC files commonly include thumbnails or collection members. WIC's
        // normal still-image path receives the container's declared primary image.
    }

    private static void ValidateImageHandle(nint handle)
    {
        int width  = HeifInterop.heif_image_handle_get_width(handle);
        int height = HeifInterop.heif_image_handle_get_height(handle);
        CheckDimensions(width, height);

        int spatialWidth  = HeifInterop.heif_image_handle_get_ispe_width(handle);
        int spatialHeight = HeifInterop.heif_image_handle_get_ispe_height(handle);
        CheckDimensions(spatialWidth, spatialHeight);
    }

    private static ImageFrame CopyFrame(nint  image,
                                        bool  hasAlpha,
                                        ulong startTicks    = 0,
                                        ulong durationTicks = 0,
                                        uint  timescale     = 1000)
    {
        ValidateDecodedImage(image);

        int width  = HeifInterop.heif_image_get_width(image, ChannelInterleaved);
        int height = HeifInterop.heif_image_get_height(image, ChannelInterleaved);
        CheckDimensions(width, height);

        byte* plane = HeifInterop.heif_image_get_plane_readonly(image, ChannelInterleaved, out int stride);
        ValidatePlane(plane, stride, width);

        ImageFrame frame = CreateFrame(width, height, startTicks, durationTicks, timescale);
        try
        {
            bool isPremultiplied = HeifInterop.heif_image_is_premultiplied_alpha(image) != 0;
            CopyPlaneToFrame(plane, stride, width, height, hasAlpha, isPremultiplied, frame.PixelSpan);
            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    private static void ValidateDecodedImage(nint image)
    {
        if (HeifInterop.heif_image_get_decoding_warnings(image, 0, 0, 0) != 0)
        {
            throw new COMException("libheif reported a decoding warning; refusing damaged input.", HResultConst.BadImage);
        }
    }

    private static void ValidatePlane(byte* plane, int stride, int width)
    {
        if (plane == null || stride < (long)width * 4)
        {
            throw new COMException("Invalid decoded HEIF-family plane.", HResultConst.BadImage);
        }
    }

    private static ImageFrame CreateFrame(int   width,
                                          int   height,
                                          ulong startTicks,
                                          ulong durationTicks,
                                          uint  timescale)
    {
        uint startMilliseconds = Milliseconds(startTicks, timescale);
        ulong endTicks         = checked(startTicks + durationTicks);
        uint endMilliseconds   = Milliseconds(endTicks, timescale);

        return new ImageFrame(width,
                              height,
                              startMilliseconds,
                              endMilliseconds - startMilliseconds,
                              startTicks,
                              durationTicks,
                              timescale);
    }

    private static void CopyPlaneToFrame(byte*      source,
                                         int        sourceStride,
                                         int        width,
                                         int        height,
                                         bool       hasAlpha,
                                         bool       isPremultiplied,
                                         Span<byte> destination)
    {
        for (int y = 0; y < height; y++)
        {
            byte* sourceRow = source + (long)y * sourceStride;
            Span<byte> destinationRow = destination.Slice(y * width * 4, width * 4);

            CopyPixelRow(sourceRow,
                         destinationRow,
                         width,
                         hasAlpha,
                         isPremultiplied);
        }
    }

    private static void CopyPixelRow(byte*      source,
                                     Span<byte> destination,
                                     int        width,
                                     bool       hasAlpha,
                                     bool       isPremultiplied)
    {
        for (int x = 0; x < width; x++)
        {
            int  sourceOffset = x * 4;
            byte alpha        = hasAlpha ? source[sourceOffset + 3] : byte.MaxValue;

            destination[sourceOffset]     = GetStraightChannel(source[sourceOffset + 2], alpha, isPremultiplied);
            destination[sourceOffset + 1] = GetStraightChannel(source[sourceOffset + 1], alpha, isPremultiplied);
            destination[sourceOffset + 2] = GetStraightChannel(source[sourceOffset], alpha, isPremultiplied);
            destination[sourceOffset + 3] = alpha;
        }
    }

    private static byte GetStraightChannel(byte value, byte alpha, bool isPremultiplied)
    {
        if (!isPremultiplied)
        {
            return value;
        }

        if (alpha == 0)
        {
            return 0;
        }

        return (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    }

    internal static uint Milliseconds(ulong ticks, uint timescale)
    {
        if (timescale == 0)
        {
            throw new COMException("Invalid HEIF-family timescale.", HResultConst.BadImage);
        }

        UInt128 value = ((UInt128)ticks * 1000 + timescale / 2) / timescale;
        if (value > int.MaxValue)
        {
            throw new COMException("HEIF-family duration exceeds the sample limit.", HResultConst.BadImage);
        }

        return (uint)value;
    }

    private static void CheckDimensions(int width, int height)
    {
        if (width <= 0 ||
            height <= 0 ||
            (long)width * height > MaxPixels)
        {
            throw new COMException("HEIF-family image exceeds the sample's 16-megapixel limit.", HResultConst.BadImage);
        }
    }

    // Validate top-level BMFF boundaries and brands, including extended-size boxes.
    // AVIF and HEVC-based HEIF share ISO-BMFF, but their WIC decoders stay
    // independent. The brand list decides which decoder may accept the stream.
    internal static bool ValidateContainer(ReadOnlySpan<byte> data,
                                           HeifContainerKind containerKind)
    {
        ValidateContainerLength(data);

        bool hasStillBrand    = false;
        bool hasSequenceBrand = false;
        bool hasMovieBox      = false;
        bool hasFileTypeBox   = false;

        for (int offset = 0; offset < data.Length;)
        {
            ReadTopLevelBox(data,
                            offset,
                            out int boxLength,
                            out int headerLength,
                            out uint boxType);

            if (offset == 0 && boxType != FileTypeBox)
            {
                throw new COMException("Expected a HEIF-family ftyp box.", HResultConst.BadImage);
            }

            switch (boxType)
            {
                case FileTypeBox when hasFileTypeBox:
                    throw new COMException("Invalid HEIF-family brand list.", HResultConst.BadImage);
                case FileTypeBox:
                    ReadBrands(data.Slice(offset, boxLength),
                               headerLength,
                               containerKind,
                               ref hasStillBrand,
                               ref hasSequenceBrand);
                    hasFileTypeBox = true;
                    break;
                case MovieBox:
                    hasMovieBox = true;
                    break;
            }

            offset = checked(offset + boxLength);
        }

        return ValidateBrands(containerKind,
                              hasStillBrand,
                              hasSequenceBrand,
                              hasMovieBox);
    }

    private static void ValidateContainerLength(ReadOnlySpan<byte> data)
    {
        if (data.Length is > EncodedInput.MaxEncodedBytes or < 16)
        {
            throw new COMException("Invalid HEIF-family length.", HResultConst.BadImage);
        }
    }

    private static void ReadTopLevelBox(ReadOnlySpan<byte> data,
                                        int                offset,
                                        out int            boxLength,
                                        out int            headerLength,
                                        out uint           boxType)
    {
        ReadOnlySpan<byte> box = data[offset..];
        if (box.Length < 8)
        {
            throw new COMException("Truncated HEIF-family box.", HResultConst.BadImage);
        }

        ulong size  = BinaryPrimitives.ReadUInt32BigEndian(box);
        headerLength = 8;

        switch (size)
        {
            case 1 when box.Length < 16:
                throw new COMException("Truncated extended HEIF-family box.", HResultConst.BadImage);
            case 1:
                size         = BinaryPrimitives.ReadUInt64BigEndian(box[8..]);
                headerLength = 16;
                break;
            case 0:
                size = (ulong)box.Length;
                break;
        }

        if (size < (uint)headerLength ||
            size > (ulong)box.Length ||
            size > int.MaxValue)
        {
            throw new COMException("Invalid HEIF-family box size.", HResultConst.BadImage);
        }

        boxLength = (int)size;
        boxType   = BinaryPrimitives.ReadUInt32BigEndian(box.Slice(4, 4));
    }

    private static void ReadBrands(ReadOnlySpan<byte> fileTypeBox,
                                   int                headerLength,
                                   HeifContainerKind  containerKind,
                                   ref bool           hasStillBrand,
                                   ref bool           hasSequenceBrand)
    {
        int payloadLength = fileTypeBox.Length - headerLength;
        if (payloadLength < 8 || payloadLength % 4 != 0)
        {
            throw new COMException("Invalid HEIF-family brand list.", HResultConst.BadImage);
        }

        for (int offset = headerLength; offset < fileTypeBox.Length; offset += 4)
        {
            if (offset == headerLength + 4)
            {
                // The second uint32 in ftyp is minor_version, not a brand.
                continue;
            }

            uint brand = BinaryPrimitives.ReadUInt32BigEndian(fileTypeBox.Slice(offset, 4));
            ClassifyBrand(brand, containerKind, ref hasStillBrand, ref hasSequenceBrand);
        }
    }

    private static void ClassifyBrand(uint              brand,
                                      HeifContainerKind containerKind,
                                      ref bool          hasStillBrand,
                                      ref bool          hasSequenceBrand)
    {
        if (containerKind == HeifContainerKind.Avif)
        {
            hasStillBrand    |= brand == AvifBrand;
            hasSequenceBrand |= brand == AvisBrand;
            return;
        }

        hasStillBrand |= brand is HeicBrand or
                                 HeixBrand or
                                 HeimBrand or
                                 HeisBrand or
                                 Mif1Brand;
        hasSequenceBrand |= brand is HevcBrand or
                                    HevxBrand or
                                    HevmBrand or
                                    HevsBrand or
                                    Msf1Brand;

        if (brand is AvifBrand or AvisBrand)
        {
            throw new COMException("An AVIF stream must be handled by the AVIF decoder.", HResultConst.BadImage);
        }
    }

    private static bool ValidateBrands(HeifContainerKind containerKind,
                                       bool              hasStillBrand,
                                       bool              hasSequenceBrand,
                                       bool              hasMovieBox)
    {
        if (!hasStillBrand && !hasSequenceBrand)
        {
            string expectedBrands = containerKind == HeifContainerKind.Avif
                ? "avif/avis"
                : "HEIF/HEIC";
            throw new COMException($"The file does not declare a supported {expectedBrands} brand.", HResultConst.BadImage);
        }

        if (hasMovieBox)
        {
            return !hasSequenceBrand ? throw new COMException("A HEIF-family sequence must declare a sequence brand.", HResultConst.BadImage) : true;
        }

        return !hasStillBrand ? throw new COMException("A HEIF-family sequence brand requires a movie box.", HResultConst.BadImage) :
            // A still HEIC may list hevc as a compatible decoder brand. The moov
            // box, rather than that compatible brand alone, identifies a sequence.
            false;
    }

    private static void Check(HeifError error)
    {
        if (error.Code == 0)
        {
            return;
        }

        string? message = Marshal.PtrToStringUTF8(error.Message);
        throw new COMException($"libheif {error.Code}/{error.Subcode}: {message}", HResultConst.BadImage);
    }
}


