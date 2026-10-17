using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Decoder;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Enums.WIC;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using Hi3Helper.Win32.Native.Structs;

namespace Hi3Helper.WICDecoder.Wic;

// Shared format-neutral WIC mechanics. This type is deliberately not COM-visible.
// Each codec has a separate COM class which delegates its WIC mechanics to this base.
internal abstract unsafe class WicDecoderBase : IDisposable
{
    private DecodedImage? _image;

    protected abstract Guid DecoderClsid    { get; }
    protected abstract Guid ContainerFormat { get; }

    protected abstract DecodedImage Decode(IStream stream);

    protected virtual void OnInitialized()
    {
    }

    ~WicDecoderBase()
    {
        Dispose();
    }

    public void Dispose()
    {
        _image?.Dispose();
        _image = null;

        GC.SuppressFinalize(this);
    }

    public HResult QueryCapability(nint streamPointer, out WICBitmapDecoderCapabilities capabilities)
    {
        capabilities = 0;
        if (streamPointer == 0)
        {
            return HResultConst.Pointer;
        }

        try
        {
            capabilities = WICBitmapDecoderCapabilities.CanDecodeAllImages;
            return 0;
        }
        catch (Exception exception)
        {
            return exception.HResult;
        }
    }

    public HResult Initialize(nint streamPointer, WICDecodeOptions cacheOptions)
    {
        HResult validationResult = ValidateInitializeArguments(streamPointer, cacheOptions);
        if (validationResult < 0)
        {
            return validationResult;
        }

        if (_image is not null)
        {
            return HResultConst.WrongState;
        }

        try
        {
            _image = DecodeStream(streamPointer);
            OnInitialized();
            return 0;
        }
        catch (Exception exception)
        {
            return exception.HResult;
        }
    }

    private static HResult ValidateInitializeArguments(nint             streamPointer,
                                                       WICDecodeOptions cacheOptions)
    {
        if (streamPointer == 0)
        {
            return HResultConst.Pointer;
        }

        if (cacheOptions is not WICDecodeOptions.WICDecodeMetadataCacheOnDemand and
            not WICDecodeOptions.WICDecodeMetadataCacheOnLoad)
        {
            return HResultConst.InvalidArg;
        }

        return 0;
    }

    private DecodedImage DecodeStream(nint streamPointer)
    {
        if (!ComMarshal<IStream>.TryCreateComObjectFromReference(streamPointer,
                                                                 out IStream? stream,
                                                                 out Exception? exception,
                                                                 releaseReference: false,
                                                                 useUnique: true))
        {
            throw exception;
        }

        try
        {
            return Decode(stream);
        }
        finally
        {
            ComMarshal<IStream>.FinalRelease(stream);
        }
    }

    public HResult GetContainerFormat(out Guid format)
    {
        format = ContainerFormat;
        return 0;
    }

    public HResult GetDecoderInfo(out nint info)
    {
        info = 0;

        try
        {
            return GetDecoderInfoCore(out info);
        }
        catch (Exception exception)
        {
            return exception.HResult;
        }
    }

    private HResult GetDecoderInfoCore(out nint info)
    {
        info = 0;
        if (!ComMarshal<IWICImagingFactory>.TryCreateComObject(in IdsConst.Factory,
                                                               CLSCTX.CLSCTX_INPROC_SERVER,
                                                               out IWICImagingFactory? factory,
                                                               out Exception? exception,
                                                               useUnique: true))
        {
            throw exception;
        }

        try
        {
            Guid decoderClsid = DecoderClsid;
            factory.CreateComponentInfo(in decoderClsid, out nint componentInfo).ThrowOnFailure();

            try
            {
                return Marshal.QueryInterface(componentInfo, in IdsConst.DecoderInfo, out info);
            }
            finally
            {
                if (componentInfo != 0)
                {
                    Marshal.Release(componentInfo);
                }
            }
        }
        finally
        {
            ComMarshal<IWICImagingFactory>.FinalRelease(factory);
        }
    }

    public HResult CopyPalette(nint palette)
    {
        return HResultConst.PaletteUnavailable;
    }

    public HResult GetMetadataQueryReader(out nint reader)
    {
        reader = 0;
        return HResultConst.Unsupported;
    }

    public HResult GetPreview(out nint source)
    {
        source = 0;
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

    public HResult GetFrameCount(out uint count)
    {
        if (_image is null)
        {
            count = 0;
            return HResultConst.WrongState;
        }

        count = (uint)_image.Frames.Length;
        return 0;
    }

    public HResult GetFrame(uint index, out nint frame)
    {
        frame = 0;
        if (_image is null)
        {
            return HResultConst.WrongState;
        }

        if (index >= _image.Frames.Length)
        {
            return HResultConst.FrameMissing;
        }

        try
        {
            // ConvertToUnmanaged creates the owned COM reference returned to WIC.
            frame = (nint)ComInterfaceMarshaller<IWICBitmapFrameDecode>.ConvertToUnmanaged(_image.Frames[index]);
            return 0;
        }
        catch (Exception exception)
        {
            return exception.HResult;
        }
    }

    public HResult GetAnimationInfo(out uint animated,
                                    out uint loopCount,
                                    out uint backgroundBgra)
    {
        animated       = 0;
        loopCount      = 0;
        backgroundBgra = 0;

        if (_image is null)
        {
            return HResultConst.WrongState;
        }

        animated       = _image.Animated ? 1u : 0u;
        loopCount      = _image.LoopCount;
        backgroundBgra = _image.BackgroundBgra;
        return 0;
    }

    public HResult GetFrameTiming(uint     index,
                                  out uint startMilliseconds,
                                  out uint durationMilliseconds)
    {
        startMilliseconds    = 0;
        durationMilliseconds = 0;

        if (!TryGetFrame(index, out ImageFrame frame, out HResult result))
        {
            return result;
        }

        startMilliseconds    = frame.StartMilliseconds;
        durationMilliseconds = frame.DurationMilliseconds;
        return 0;
    }

    public HResult GetFrameTimingTicks(uint index,
                                       out ulong startTicks,
                                       out ulong durationTicks,
                                       out uint timescale)
    {
        startTicks    = 0;
        durationTicks = 0;
        timescale     = 0;

        if (!TryGetFrame(index, out ImageFrame frame, out HResult result))
        {
            return result;
        }

        startTicks    = frame.StartTicks;
        durationTicks = frame.DurationTicks;
        timescale     = frame.Timescale;
        return 0;
    }

    private bool TryGetFrame(uint           index,
                             out ImageFrame frame,
                             out HResult    result)
    {
        frame  = null!;
        result = 0;

        if (_image is null)
        {
            result = HResultConst.WrongState;
            return false;
        }

        if (index >= _image.Frames.Length)
        {
            result = HResultConst.FrameMissing;
            return false;
        }

        ImageFrame? selectedFrame = _image.Frames[index];
        if (selectedFrame is null)
        {
            result = HResultConst.FrameMissing;
            return false;
        }

        frame = selectedFrame;
        return true;
    }
}
