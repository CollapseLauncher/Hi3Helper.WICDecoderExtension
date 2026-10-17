using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

namespace Hi3Helper.WICDecoder.Decoder.Webp;


internal static partial class WebpInterop
{
    public const int    DemuxAbiVersion = 0x0107; // libwebp 1.6.0, webp/demux.h
    public const string LibwebpName     = "libwebp";
    public const string WebpDemuxName   = "webpdemux";

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint WebPDemuxInternal(
        ref readonly WebpData data,
        int                   partial,
        ref int               state,
        int                   version);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint WebPDemuxGetI(nint demux, int feature);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void WebPDemuxDelete(nint demux);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int WebPAnimDecoderOptionsInitInternal(
        ref WebpAnimDecoderOptions options,
        int                        version);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint WebPAnimDecoderNewInternal(
        ref readonly WebpData               data,
        ref          WebpAnimDecoderOptions options,
        int                                 version);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int WebPAnimDecoderGetNext(
        nint     decoder,
        out nint buffer,
        out int  timestamp);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int WebPAnimDecoderHasMoreFrames(nint decoder);

    [LibraryImport(WebpDemuxName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void WebPAnimDecoderDelete(nint decoder);
}
