using Hi3Helper.Win32.Native.Structs;

// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Const;

internal static class HResultConst
{
    public static readonly HResult Pointer            = unchecked((int)0x80004003);
    public static readonly HResult InvalidArg         = unchecked((int)0x80070057);
    public static readonly HResult WrongState         = unchecked((int)0x88982F04);
    public static readonly HResult PaletteUnavailable = unchecked((int)0x88982F45);
    public static readonly HResult BadImage           = unchecked((int)0x88982F60);
    public static readonly HResult FrameMissing       = unchecked((int)0x88982F62);
    public static readonly HResult Unsupported        = unchecked((int)0x88982F81);
    public static readonly HResult InsufficientBuffer = unchecked((int)0x88982F8C);
    public static readonly HResult NoAggregation      = unchecked((int)0x80040110);
}