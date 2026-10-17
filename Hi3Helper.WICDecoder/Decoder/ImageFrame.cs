using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.Native.Structs.D2D;

namespace Hi3Helper.WICDecoder.Decoder;

[GeneratedComClass]
internal sealed unsafe partial class ImageFrame : IWICBitmapFrameDecode, IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly int   _width;
    private readonly int   _height;
    private readonly nuint _bufferLength;
    private          byte* _buffer;

    internal ImageFrame(int    width,
                        int    height,
                        uint   startMilliseconds    = 0,
                        uint   durationMilliseconds = 0,
                        ulong? startTicks           = null,
                        ulong? durationTicks        = null,
                        uint   timescale            = 1000)
    {
        _width        = width;
        _height       = height;
        _bufferLength = CalculateBufferLength(width, height);
        _buffer       = AllocateBuffer(_bufferLength);

        StartMilliseconds    = startMilliseconds;
        DurationMilliseconds = durationMilliseconds;
        StartTicks           = startTicks ?? startMilliseconds;
        DurationTicks        = durationTicks ?? durationMilliseconds;
        Timescale            = timescale;
    }

    ~ImageFrame()
    {
        Dispose();
    }

    public uint  StartMilliseconds    { get; }
    public uint  DurationMilliseconds { get; }
    public ulong StartTicks           { get; }
    public ulong DurationTicks        { get; }
    public uint  Timescale            { get; }

    internal Span<byte> PixelSpan         => new(_buffer, checked((int)_bufferLength));
    internal nint       PixelBuffer       => (nint)_buffer;
    internal nuint      PixelBufferLength => _bufferLength;

    public void Dispose()
    {
        byte* buffer = _buffer;
        _buffer = null;

        if (buffer != null)
        {
            NativeMemory.Free(buffer);
        }

        GC.SuppressFinalize(this);
    }

    private static nuint CalculateBufferLength(int width, int height)
    {
        ulong length = checked((ulong)(uint)width *
                               (uint)height *
                               BytesPerPixel);
        return checked((nuint)length);
    }

    private static byte* AllocateBuffer(nuint length)
    {
        byte* buffer = (byte*)NativeMemory.Alloc(length);
        if (buffer == null)
        {
            throw new OutOfMemoryException();
        }

        return buffer;
    }

    public HResult GetSize(out uint width, out uint height)
    {
        width  = (uint)_width;
        height = (uint)_height;
        return 0;
    }

    public HResult GetPixelFormat(out Guid format)
    {
        format = IdsConst.Bgra;
        return 0;
    }

    public HResult GetResolution(out double x, out double y)
    {
        x = 96;
        y = 96;
        return 0;
    }

    public HResult CopyPalette(nint palette)
    {
        return HResultConst.PaletteUnavailable;
    }

    public HResult CopyPixels(WICRect* rectangle,
                              uint stride,
                              uint bufferSize,
                              byte* buffer)
    {
        if (buffer == null)
        {
            return HResultConst.Pointer;
        }

        WICRect sourceRectangle = GetSourceRectangle(rectangle);
        HResult validationResult = ValidateCopyArguments(in sourceRectangle,
                                                         stride,
                                                         bufferSize);
        if (validationResult < 0)
        {
            return validationResult;
        }

        CopyRows(in sourceRectangle,
                 stride,
                 buffer);
        return 0;
    }

    private WICRect GetSourceRectangle(WICRect* rectangle)
    {
        if (rectangle != null)
        {
            return *rectangle;
        }

        return new WICRect
        {
            Width  = _width,
            Height = _height
        };
    }

    private HResult ValidateCopyArguments(in WICRect rectangle,
                                          uint stride,
                                          uint bufferSize)
    {
        if (rectangle.X < 0 ||
            rectangle.Y < 0 ||
            rectangle.Width <= 0 ||
            rectangle.Height <= 0 ||
            (long)rectangle.X + rectangle.Width > _width ||
            (long)rectangle.Y + rectangle.Height > _height)
        {
            return HResultConst.InvalidArg;
        }

        ulong rowBytes = (ulong)rectangle.Width * BytesPerPixel;
        if (stride < rowBytes)
        {
            return HResultConst.InvalidArg;
        }

        ulong requiredBufferSize =
            (ulong)stride * (uint)(rectangle.Height - 1) + rowBytes;
        if (bufferSize < requiredBufferSize)
        {
            return HResultConst.InsufficientBuffer;
        }

        return 0;
    }

    private void CopyRows(in WICRect rectangle,
                          uint       destinationStride,
                          byte*      destination)
    {
        long rowBytes = (long)rectangle.Width * BytesPerPixel;

        for (int row = 0; row < rectangle.Height; row++)
        {
            long  sourceOffset      = ((long)(rectangle.Y + row) * _width + rectangle.X) * BytesPerPixel;
            ulong destinationOffset = (ulong)(uint)row * destinationStride;

            Buffer.MemoryCopy(_buffer + sourceOffset,
                              destination + (nuint)destinationOffset,
                              rowBytes,
                              rowBytes);
        }
    }

    public HResult GetMetadataQueryReader(out nint reader)
    {
        reader = 0;
        return HResultConst.Unsupported;
    }

    public HResult GetColorContexts(uint     count,
                                    nint*    contexts,
                                    out uint actualCount)
    {
        actualCount = 0;
        return 0;
    }

    public HResult GetThumbnail(out nint source)
    {
        source = 0;
        return HResultConst.Unsupported;
    }
}
