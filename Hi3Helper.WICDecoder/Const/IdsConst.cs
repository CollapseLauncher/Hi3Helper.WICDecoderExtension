using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Const;

internal static class IdsConst
{
    public static readonly Guid WebpDecoder     = new("CA3F19D7-6E08-48CD-BC9D-28F51287202F");
    public static readonly Guid WebpContainer   = new("E094B0E2-67F2-45B3-B0EA-115337CA7CF3");
    public static readonly Guid AvifDecoder     = new("DC52B8A8-3B55-4D44-A9AC-D0A790AB3DAF");
    public static readonly Guid AvifContainer   = new("F3F0C4C0-E479-4A7B-A53F-F8A667FC919B");
    public static readonly Guid HeifDecoder     = new("C9B7A3DC-88D6-4E18-86DF-8F8A9ACD37E1");
    public static readonly Guid HeifContainer   = new("715CDAC5-80E4-4D70-9A3F-B6A7C67FD802");
    public static readonly Guid JpegXlDecoder   = new("192E0D7B-BAFA-4DCD-8F4B-F086B233CEA7");
    public static readonly Guid JpegXlContainer = new("7FCD977F-9BB3-4927-850C-F5BEBCFD95C1");

    public static readonly Guid Vendor      = new("563C717D-924C-4418-94C7-09E330E46D78");
    public static readonly Guid Bgra        = new("6FDDC324-4E03-4BFE-B185-3D77768DC90F");
    public static readonly Guid Factory     = new("CACAF262-9370-4615-A13B-9F5539DA4C0A");
    public static readonly Guid DecoderInfo = new("D8CD007F-D08F-4191-9BFC-236EA7F0E4B5");
}
