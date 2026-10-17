using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

// The complete heif.dll boundary lives here. The decoder backends only contain
// container validation, lifetime management, and pixel-copying logic.
internal static unsafe partial class HeifInterop
{
    private const string LibraryName = "heif";

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_init(nint options);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_deinit();

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint heif_context_alloc();

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_context_free(nint context);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_context_read_from_memory_without_copy(
        nint  context,
        nint  data,
        nuint length,
        nint  options);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_context_get_number_of_top_level_images(nint context);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_context_get_primary_image_handle(
        nint     context,
        out nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_context_has_sequence(nint context);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_context_number_of_sequence_tracks(nint context);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_context_get_track_ids(nint context, ref uint ids);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint heif_context_get_track(nint context, uint id);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_image_handle_release(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_handle_get_width(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_handle_get_height(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_handle_get_ispe_width(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_handle_get_ispe_height(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_handle_has_alpha_channel(nint handle);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_decode_image(
        nint     handle,
        out nint image,
        int      colorspace,
        int      chroma,
        nint     options);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_image_release(nint image);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_get_width(nint image, int channel);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_get_height(nint image, int channel);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial byte* heif_image_get_plane_readonly(
        nint    image,
        int     channel,
        out int stride);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_is_premultiplied_alpha(nint image);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_image_get_decoding_warnings(
        nint image,
        int  first,
        nint warnings,
        int  count);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint heif_image_get_duration(nint image);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_track_release(nint track);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint heif_track_get_track_handler_type(nint track);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_track_get_image_resolution(
        nint       track,
        out ushort width,
        out ushort height);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint heif_track_get_timescale(nint track);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int heif_track_has_alpha_channel(nint track);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifError heif_track_decode_next_image(
        nint                       track,
        out nint                   image,
        int                        colorspace,
        int                        chroma,
        HeifDecodingOptionsPrefix* options);

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial HeifDecodingOptionsPrefix* heif_decoding_options_alloc();

    [LibraryImport(LibraryName), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void heif_decoding_options_free(HeifDecodingOptionsPrefix* options);
}
