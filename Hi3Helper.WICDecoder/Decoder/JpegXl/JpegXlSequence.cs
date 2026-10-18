using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

internal sealed class JpegXlSequence : IDisposable
{
    private readonly List<ImageFrame?> _frames = [];
    private readonly int               _maximumFrames;
    private readonly int               _maximumPixelBytes;

    private JxlBasicInfo   _information;
    private JxlFrameHeader _frameHeader;
    private ImageFrame?    _currentFrame;
    private ulong          _startTicks;
    private bool           _hasInformation;
    private bool           _hasFrameHeader;
    private bool           _detached;

    internal JpegXlSequence(int maximumFrames,
                            int maximumPixelBytes)
    {
        _maximumFrames     = maximumFrames;
        _maximumPixelBytes = maximumPixelBytes;
    }

    internal nint FrameBuffer => _currentFrame?.PixelBuffer ?? nint.Zero;

    public void Dispose()
    {
        if (_detached)
        {
            return;
        }

        _currentFrame?.Dispose();
        _currentFrame = null;

        foreach (ImageFrame? frame in _frames)
        {
            frame?.Dispose();
        }
    }

    internal void SetBasicInformation(in JxlBasicInfo information)
    {
        if (_hasInformation)
        {
            throw new COMException("JPEG XL supplied its image header more than once.", HResultConst.BadImage);
        }

        ValidateDimensions(in information);
        ValidateAnimationTimebase(in information);

        _information    = information;
        _hasInformation = true;
    }

    internal void BeginFrame(in JxlFrameHeader header)
    {
        EnsureBasicInformation();

        if (_currentFrame is not null || _hasFrameHeader)
        {
            throw new COMException("JPEG XL started a frame before the previous frame completed.", HResultConst.BadImage);
        }

        if (_frames.Count >= _maximumFrames)
        {
            throw new COMException("The JPEG XL animation exceeds the sample frame limit.", HResultConst.BadImage);
        }

        _frameHeader    = header;
        _hasFrameHeader = true;
    }

    internal void AllocateFrame(nuint requiredSize)
    {
        EnsureBasicInformation();

        if (!_hasFrameHeader || _currentFrame is not null)
        {
            throw new COMException("libjxl requested a frame buffer in an invalid state.", HResultConst.BadImage);
        }

        nuint expectedSize = CalculateExpectedBufferSize();
        if (requiredSize != expectedSize ||
            requiredSize > (nuint)_maximumPixelBytes)
        {
            throw new COMException("The JPEG XL frame buffer has an unsupported size.", HResultConst.BadImage);
        }

        ReadTiming(out ulong durationTicks,
                   out uint  durationMilliseconds,
                   out uint  timescale);

        _currentFrame = new ImageFrame(
            checked((int)_information.Width),
            checked((int)_information.Height),
            ConvertToMilliseconds(_startTicks, timescale),
            durationMilliseconds,
            _startTicks,
            durationTicks,
            timescale);
    }

    internal void CompleteFrame()
    {
        if (_currentFrame is null)
        {
            throw new COMException("libjxl completed a frame without providing pixels.", HResultConst.BadImage);
        }

        BufferHelper.ReverseXxxa(_currentFrame.PixelSpan);

        _frames.Add(_currentFrame);
        _startTicks = checked(_startTicks + _currentFrame.DurationTicks);
        _currentFrame = null;
        _hasFrameHeader = false;
    }

    internal void ValidateComplete()
    {
        EnsureBasicInformation();

        if (_currentFrame is not null || _hasFrameHeader)
        {
            throw new COMException("The JPEG XL stream ended during a frame.", HResultConst.BadImage);
        }

        if (_frames.Count == 0)
        {
            throw new COMException("The JPEG XL stream did not contain a displayable frame.", HResultConst.BadImage);
        }
    }

    internal DecodedImage DetachImage()
    {
        ValidateComplete();

        _detached = true;
        return new DecodedImage([.. _frames], _information.HaveAnimation != 0, _information.Animation.LoopCount, 0);
    }

    private void EnsureBasicInformation()
    {
        if (!_hasInformation)
        {
            throw new COMException("libjxl supplied frame data before the image header.", HResultConst.BadImage);
        }
    }

    private void ValidateDimensions(in JxlBasicInfo information)
    {
        if (information.Width == 0 || information.Height == 0 ||
            information.Width > int.MaxValue ||
            information.Height > int.MaxValue)
        {
            throw new COMException("The JPEG XL image dimensions are invalid.", HResultConst.BadImage);
        }

        ulong pixelBytes = checked(
            (ulong)information.Width * information.Height * 4);
        if (pixelBytes > (ulong)_maximumPixelBytes)
        {
            throw new COMException("The JPEG XL image exceeds the sample pixel limit.", HResultConst.BadImage);
        }
    }

    private static void ValidateAnimationTimebase(
        in JxlBasicInfo information)
    {
        if (information.HaveAnimation == 0)
        {
            return;
        }

        if (information.Animation.TicksPerSecondNumerator == 0 ||
            information.Animation.TicksPerSecondDenominator == 0)
        {
            throw new COMException("The JPEG XL animation timebase is invalid.", HResultConst.BadImage);
        }
    }

    private nuint CalculateExpectedBufferSize()
    {
        ulong size = checked((ulong)_information.Width * _information.Height * 4);
        return checked((nuint)size);
    }

    private void ReadTiming(out ulong durationTicks,
                            out uint  durationMilliseconds,
                            out uint  timescale)
    {
        if (_information.HaveAnimation == 0)
        {
            durationTicks        = 0;
            durationMilliseconds = 0;
            timescale            = 1000;
            return;
        }

        timescale            = _information.Animation.TicksPerSecondNumerator;
        durationTicks        = checked((ulong)_frameHeader.Duration * _information.Animation.TicksPerSecondDenominator);
        durationMilliseconds = ConvertToMilliseconds(durationTicks, timescale);
    }

    private static uint ConvertToMilliseconds(ulong ticks,
                                              uint  timescale)
    {
        ulong milliseconds = checked(ticks * 1000) / timescale;
        return checked((uint)milliseconds);
    }
}
