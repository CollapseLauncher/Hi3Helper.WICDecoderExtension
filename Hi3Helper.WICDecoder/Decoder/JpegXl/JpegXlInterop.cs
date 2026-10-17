using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

// This boundary matches libjxl 0.12.0, supplied by
// PhotoSauce.NativeCodecs.Libjxl 0.12.0-preview1.
internal static partial class JpegXlInterop
{
    private const string LibraryName = "jxl";

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint JxlDecoderVersion();

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlSignature JxlSignatureCheck(nint data, nuint length);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint JxlDecoderCreate(nint memoryManager);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void JxlDecoderDestroy(nint decoder);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderSubscribeEvents(
        nint             decoder,
        JxlDecoderStatus eventsWanted);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderSetCoalescing(
        nint decoder,
        int  coalescing);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderSetUnpremultiplyAlpha(
        nint decoder,
        int  unpremultiplyAlpha);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderSetInput(
        nint  decoder,
        nint  data,
        nuint length);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void JxlDecoderCloseInput(nint decoder);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderProcessInput(nint decoder);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderGetBasicInfo(
        nint             decoder,
        out JxlBasicInfo information);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderGetFrameHeader(
        nint               decoder,
        out JxlFrameHeader header);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderImageOutBufferSize(
        nint               decoder,
        in  JxlPixelFormat format,
        out nuint          size);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial JxlDecoderStatus JxlDecoderSetImageOutBuffer(
        nint              decoder,
        in JxlPixelFormat format,
        nint              buffer,
        nuint             size);
}
