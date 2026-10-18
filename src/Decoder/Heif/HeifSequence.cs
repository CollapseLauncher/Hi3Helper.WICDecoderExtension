using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;


// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

internal static unsafe partial class HeifBackend
{
    private const uint PictureTrack = 0x70696374; // pict
    private const uint VideoTrack   = 0x76696465; // vide

    private const int  MaxFrames        = 4096;
    private const int  MaxTracks        = 64;
    private const long MaxSequenceBytes = 256 * 1024 * 1024;

    private static DecodedImage DecodeSequence(nint context, ReadOnlySpan<byte> encoded)
    {
        ValidateSequencePresence(context);

        nint visualTrack = FindVisualTrack(context, out uint visualTrackId);
        try
        {
            GetSequenceDimensions(visualTrack,
                                  out int width,
                                  out int height);

            uint         timescale = HeifInterop.heif_track_get_timescale(visualTrack);
            HeifTimeline timeline  = HeifTimeline.Read(encoded, visualTrackId);

            ValidateSequenceMetadata(width,
                                     height,
                                     timescale,
                                     timeline);

            HeifDecodingOptionsPrefix* options = CreateSequenceDecodingOptions();
            try
            {
                return DecodeSequenceFrames(visualTrack,
                                            options,
                                            width,
                                            height,
                                            timescale,
                                            timeline);
            }
            finally
            {
                HeifInterop.heif_decoding_options_free(options);
            }
        }
        finally
        {
            HeifInterop.heif_track_release(visualTrack);
        }
    }

    private static void ValidateSequencePresence(nint context)
    {
        if (HeifInterop.heif_context_has_sequence(context) == 0)
        {
            throw new COMException("Missing HEIF-family sequence.", HResultConst.BadImage);
        }
    }

    private static nint FindVisualTrack(nint context, out uint visualTrackId)
    {
        int trackCount = HeifInterop.heif_context_number_of_sequence_tracks(context);
        if (trackCount is <= 0 or > MaxTracks)
        {
            throw new COMException("Invalid HEIF-family track count.", HResultConst.BadImage);
        }

        Span<uint> trackIds = stackalloc uint[trackCount];
        HeifInterop.heif_context_get_track_ids(
            context,
            ref MemoryMarshal.GetReference(trackIds));

        nint visualTrack = 0;
        visualTrackId = 0;

        try
        {
            foreach (uint trackId in trackIds)
            {
                nint track = HeifInterop.heif_context_get_track(context, trackId);
                if (track == 0)
                {
                    throw new COMException("Missing HEIF-family track.", HResultConst.BadImage);
                }

                if (!IsVisualTrack(track))
                {
                    HeifInterop.heif_track_release(track);
                    continue;
                }

                if (visualTrack != 0)
                {
                    HeifInterop.heif_track_release(track);
                    throw new COMException("Multiple visual HEIF-family tracks are unsupported.", HResultConst.Unsupported);
                }

                visualTrack   = track;
                visualTrackId = trackId;
            }

            if (visualTrack == 0)
            {
                throw new COMException("No visual HEIF-family track.", HResultConst.BadImage);
            }

            return visualTrack;
        }
        catch
        {
            if (visualTrack != 0)
            {
                HeifInterop.heif_track_release(visualTrack);
            }

            throw;
        }
    }

    private static bool IsVisualTrack(nint track)
    {
        uint handlerType = HeifInterop.heif_track_get_track_handler_type(track);
        return handlerType is PictureTrack or VideoTrack;
    }

    private static void GetSequenceDimensions(nint track,
                                              out int width,
                                              out int height)
    {
        Check(HeifInterop.heif_track_get_image_resolution(track, out ushort nativeWidth, out ushort nativeHeight));
        width  = nativeWidth;
        height = nativeHeight;
        CheckDimensions(width, height);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static void ValidateSequenceMetadata(int          width,
                                                 int          height,
                                                 uint         timescale,
                                                 HeifTimeline timeline)
    {
        if (timeline.Timescale != timescale)
        {
            throw new COMException("Inconsistent HEIF-family timescale.", HResultConst.BadImage);
        }

        long canvasBytes   = (long)width * height * 4;
        long requiredBytes = canvasBytes * (timeline.Durations.Length + 2L);

        if (requiredBytes > MaxSequenceBytes)
        {
            throw new COMException("HEIF-family sequence exceeds the pixel-memory limit.", HResultConst.BadImage);
        }
    }

    private static HeifDecodingOptionsPrefix* CreateSequenceDecodingOptions()
    {
        HeifDecodingOptionsPrefix* options = HeifInterop.heif_decoding_options_alloc();
        if (options == null)
        {
            throw new OutOfMemoryException();
        }

        if (options->Version < 8)
        {
            HeifInterop.heif_decoding_options_free(options);
            throw new COMException("libheif sequence options require version 8 or newer.", HResultConst.Unsupported);
        }

        // Decode the media samples once. The WIC consumer controls playback loops.
        options->IgnoreSequenceEditlist = 1;
        options->StrictDecoding         = 1;
        return options;
    }

    private static DecodedImage DecodeSequenceFrames(
        nint                       track,
        HeifDecodingOptionsPrefix* options,
        int                        width,
        int                        height,
        uint                       timescale,
        HeifTimeline               timeline)
    {
        List<ImageFrame> frames        = [];
        ulong            timestamp     = 0;
        long             retainedBytes = 0;
        bool             hasAlpha      = HeifInterop.heif_track_has_alpha_channel(track) != 0;

        try
        {
            while (TryDecodeNextSequenceImage(track, options, out nint image))
            {
                try
                {
                    ImageFrame frame = DecodeSequenceFrame(image,
                                                           hasAlpha,
                                                           width,
                                                           height,
                                                           timescale,
                                                           timeline,
                                                           frames.Count,
                                                           timestamp,
                                                           retainedBytes,
                                                           out uint duration,
                                                           out long frameBytes);

                    frames.Add(frame);
                    timestamp     = checked(timestamp + duration);
                    retainedBytes = checked(retainedBytes + frameBytes);
                }
                finally
                {
                    HeifInterop.heif_image_release(image);
                }
            }

            ValidateDecodedFrameCount(frames.Count, timeline.Durations.Length);
            return new DecodedImage([.. frames], true, timeline.LoopCount, 0);
        }
        catch
        {
            DisposeFrames(frames);
            throw;
        }
    }

    private static bool TryDecodeNextSequenceImage(
        nint                       track,
        HeifDecodingOptionsPrefix* options,
        out nint                   image)
    {
        HeifError error = HeifInterop.heif_track_decode_next_image(
            track,
            out image,
            RgbColorSpace,
            ChromaRgba,
            options);

        if (error.Code == 13) // heif_error_End_of_sequence
        {
            if (image != 0)
            {
                HeifInterop.heif_image_release(image);
            }

            image = 0;
            return false;
        }

        try
        {
            Check(error);
        }
        catch
        {
            if (image == 0)
            {
                throw;
            }

            HeifInterop.heif_image_release(image);
            image = 0;
            throw;
        }

        if (image == 0)
        {
            throw new COMException("Missing HEIF-family frame.", HResultConst.BadImage);
        }

        return true;
    }

    private static ImageFrame DecodeSequenceFrame(nint         image,
                                                  bool         hasAlpha,
                                                  int          expectedWidth,
                                                  int          expectedHeight,
                                                  uint         timescale,
                                                  HeifTimeline timeline,
                                                  int          frameIndex,
                                                  ulong        timestamp,
                                                  long         retainedBytes,
                                                  out uint     duration,
                                                  out long     frameBytes)
    {
        int width  = HeifInterop.heif_image_get_width(image, ChannelInterleaved);
        int height = HeifInterop.heif_image_get_height(image, ChannelInterleaved);
        CheckDimensions(width, height);

        ValidateSequenceCanvas(width, height, expectedWidth, expectedHeight);
        frameBytes = (long)width * height * 4;
        ValidateSequenceMemory(frameIndex, retainedBytes, frameBytes);

        duration = HeifInterop.heif_image_get_duration(image);
        ValidateFrameDuration(frameIndex, duration, timeline.Durations);

        return CopyFrame(image, hasAlpha, timestamp, duration, timescale);
    }

    private static void ValidateSequenceCanvas(int width,
                                               int height,
                                               int expectedWidth,
                                               int expectedHeight)
    {
        if (width != expectedWidth || height != expectedHeight)
        {
            throw new COMException("HEIF-family sequence changes canvas size.", HResultConst.Unsupported);
        }
    }

    private static void ValidateSequenceMemory(int  frameIndex,
                                               long retainedBytes,
                                               long frameBytes)
    {
        // Keep room for the retained snapshots and libheif's two working canvases.
        if (frameIndex >= MaxFrames ||
            retainedBytes + frameBytes * 3 > MaxSequenceBytes)
        {
            throw new COMException("HEIF-family sequence exceeds the frame/pixel-memory limit.", HResultConst.BadImage);
        }
    }

    private static void ValidateFrameDuration(int                frameIndex,
                                              uint               duration,
                                              ReadOnlySpan<uint> expectedDurations)
    {
        if (frameIndex >= expectedDurations.Length ||
            duration != expectedDurations[frameIndex])
        {
            throw new COMException("Inconsistent HEIF-family sample timing/count.", HResultConst.BadImage);
        }
    }

    private static void ValidateDecodedFrameCount(int decodedCount,
                                                  int expectedCount)
    {
        if (decodedCount != expectedCount)
        {
            throw new COMException("Incomplete HEIF-family sequence.", HResultConst.BadImage);
        }
    }

    private static void DisposeFrames(List<ImageFrame> frames)
    {
        foreach (ImageFrame frame in frames)
        {
            frame.Dispose();
        }
    }
}
