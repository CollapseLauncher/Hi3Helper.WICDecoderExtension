using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.Win32.Native.Interfaces;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Webp;

internal static unsafe class WebpBackend
{
    private const uint AnimationFlag   = 2;
    private const int  ModeBgra        = 3;
    private const int  MaxEncodedBytes = EncodedInput.MaxEncodedBytes;
    private const int  MaxPixelBytes   = 256 * 1024 * 1024;
    private const int  MaxFrames       = 4096;

    public static DecodedImage Decode(IStream stream)
    {
        using EncodedInput encoded = EncodedInput.Read(stream);
        ValidateContainer(encoded.Span);

        WebpData data = CreateWebpData(encoded);
        WebpInfo info = ReadWebpInfo(in data);
        ValidateWebpInfo(info);

        nint decoder = CreateAnimationDecoder(in data);
        try
        {
            ImageFrame?[] frames = DecodeFrames(decoder, info);
            bool isAnimated      = (info.FormatFlags & AnimationFlag) != 0;

            return new DecodedImage(frames,
                                    isAnimated,
                                    info.LoopCount,
                                    info.BackgroundColor);
        }
        finally
        {
            WebpInterop.WebPAnimDecoderDelete(decoder);
        }
    }

    private static void ValidateContainer(ReadOnlySpan<byte> data)
    {
        ValidateRiffHeader(data);
        ValidateChunks(data);
    }

    private static void ValidateRiffHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaxEncodedBytes ||
            data.Length < 12 ||
            !data[..4].SequenceEqual("RIFF"u8) ||
            !data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            throw new COMException("Invalid or truncated WebP container.", HResultConst.BadImage);
        }

        uint  riffPayloadLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        ulong declaredLength    = (ulong)riffPayloadLength + 8;

        if (declaredLength != (ulong)data.Length)
        {
            throw new COMException("Invalid or truncated WebP container.", HResultConst.BadImage);
        }
    }

    private static void ValidateChunks(ReadOnlySpan<byte> data)
    {
        long totalAnimationDuration = 0;

        for (long offset = 12; offset < data.Length;)
        {
            ReadChunk(data, offset, out int chunkOffset, out uint chunkLength, out long nextOffset);
            ReadOnlySpan<byte> chunkType = data.Slice(chunkOffset, 4);
            if (chunkType.SequenceEqual("ANMF"u8))
            {
                uint frameDuration = ReadAnimationFrameDuration(data, chunkOffset, chunkLength);
                totalAnimationDuration += frameDuration;
                ValidateAnimationDuration(totalAnimationDuration);
            }

            offset = nextOffset;
        }
    }

    private static void ReadChunk(ReadOnlySpan<byte> data,
                                  long               offset,
                                  out int            chunkOffset,
                                  out uint           chunkLength,
                                  out long           nextOffset)
    {
        if (data.Length - offset < 8)
        {
            throw new COMException("Truncated WebP chunk.", HResultConst.BadImage);
        }

        chunkOffset = checked((int)offset);
        chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(chunkOffset + 4, 4));

        long dataEnd = checked(offset + 8 + chunkLength);
        nextOffset   = checked(dataEnd + (chunkLength & 1));

        if (nextOffset > data.Length)
        {
            throw new COMException("Truncated WebP chunk data.", HResultConst.BadImage);
        }
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static uint ReadAnimationFrameDuration(ReadOnlySpan<byte> data,
                                                   int                chunkOffset,
                                                   uint               chunkLength)
    {
        if (chunkLength < 16)
        {
            throw new COMException("Truncated WebP animation header.", HResultConst.BadImage);
        }

        // ANMF payload offset 12 stores the frame duration as uint24 little-endian.
        int durationOffset = chunkOffset + 20;
        return (uint)(data[durationOffset] |
                      (data[durationOffset + 1] << 8) |
                      (data[durationOffset + 2] << 16));
    }

    private static void ValidateAnimationDuration(long duration)
    {
        if (duration > int.MaxValue)
        {
            throw new COMException("Animation duration exceeds libwebp's timestamp range.", HResultConst.BadImage);
        }
    }

    private static WebpData CreateWebpData(EncodedInput encoded)
    {
        return new WebpData
        {
            Bytes = encoded.Buffer,
            Size  = encoded.Length
        };
    }

    private static WebpInfo ReadWebpInfo(ref readonly WebpData data)
    {
        nint demux = WebpInterop.WebPDemuxInternal(in data, 0, ref Unsafe.NullRef<int>(), WebpInterop.DemuxAbiVersion);
        if (demux == 0)
        {
            throw new COMException("Invalid WebP container.", HResultConst.BadImage);
        }

        try
        {
            uint  formatFlags     = WebpInterop.WebPDemuxGetI(demux, 0);
            uint  width           = WebpInterop.WebPDemuxGetI(demux, 1);
            uint  height          = WebpInterop.WebPDemuxGetI(demux, 2);
            uint  loopCount       = WebpInterop.WebPDemuxGetI(demux, 3);
            uint  backgroundColor = WebpInterop.WebPDemuxGetI(demux, 4);
            uint  frameCount      = WebpInterop.WebPDemuxGetI(demux, 5);
            ulong canvasBytes     = (ulong)width * height * 4;

            return new WebpInfo(width,
                                height,
                                frameCount,
                                loopCount,
                                backgroundColor,
                                formatFlags,
                                canvasBytes);
        }
        finally
        {
            WebpInterop.WebPDemuxDelete(demux);
        }
    }

    private static void ValidateWebpInfo(WebpInfo info)
    {
        if (info.Width == 0 ||
            info.Height == 0 ||
            info.FrameCount == 0 ||
            info.FrameCount > MaxFrames ||
            info.CanvasBytes > MaxPixelBytes ||
            info.CanvasBytes * (info.FrameCount + 2UL) > MaxPixelBytes)
        {
            throw new COMException("Image/animation exceeds the sample's frame or pixel-memory limit.", HResultConst.BadImage);
        }
    }

    private static nint CreateAnimationDecoder(ref readonly WebpData data)
    {
        WebpAnimDecoderOptions options = CreateDecoderOptions();
        nint decoder = WebpInterop.WebPAnimDecoderNewInternal(in data, ref options, WebpInterop.DemuxAbiVersion);
        return decoder == 0 ? throw new COMException($"{WebpInterop.LibwebpName} could not open the image.", HResultConst.BadImage) : decoder;
    }

    private static WebpAnimDecoderOptions CreateDecoderOptions()
    {
        WebpAnimDecoderOptions options = default;
        if (WebpInterop.WebPAnimDecoderOptionsInitInternal(ref options, WebpInterop.DemuxAbiVersion) == 0)
        {
            throw new COMException($"Incompatible {WebpInterop.LibwebpName} demux ABI.", HResultConst.BadImage);
        }

        // MODE_BGRA uses straight alpha. MODE_bgrA would return premultiplied alpha.
        options.ColorMode = ModeBgra;
        return options;
    }

    private static ImageFrame?[] DecodeFrames(nint decoder, WebpInfo info)
    {
        ImageFrame?[] frames = new ImageFrame[checked((int)info.FrameCount)];
        int previousTimestamp = 0;

        try
        {
            for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                ImageFrame frame = DecodeFrame(decoder, info, previousTimestamp, out int timestamp);

                frames[frameIndex] = frame;
                previousTimestamp  = timestamp;
            }

            ValidateFrameCount(decoder);
            return frames;
        }
        catch
        {
            DisposeFrames(frames);
            throw;
        }
    }

    private static ImageFrame DecodeFrame(nint     decoder,
                                          WebpInfo info,
                                          int      previousTimestamp,
                                          out int  timestamp)
    {
        if (WebpInterop.WebPAnimDecoderGetNext(decoder, out nint source, out timestamp) == 0 ||
            source == 0 ||
            timestamp < previousTimestamp)
        {
            throw new COMException("Invalid or truncated WebP animation frame.", HResultConst.BadImage);
        }

        uint       start    = (uint)previousTimestamp;
        uint       duration = (uint)(timestamp - previousTimestamp);
        ImageFrame frame    = new((int)info.Width, (int)info.Height, start, duration);

        try
        {
            ReadOnlySpan<byte> sourcePixels = new((void*)source, checked((int)info.CanvasBytes));
            sourcePixels.CopyTo(frame.PixelSpan);
            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    private static void ValidateFrameCount(nint decoder)
    {
        if (WebpInterop.WebPAnimDecoderHasMoreFrames(decoder) != 0)
        {
            throw new COMException("Inconsistent WebP animation frame count.", HResultConst.BadImage);
        }
    }

    private static void DisposeFrames(ImageFrame?[] frames)
    {
        foreach (ImageFrame? frame in frames)
        {
            frame?.Dispose();
        }
    }
}
