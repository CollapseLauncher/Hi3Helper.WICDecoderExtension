using System;

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

[Flags]
internal enum JxlDecoderStatus
{
    Success            = 0,
    Error              = 1,
    NeedMoreInput      = 2,
    NeedImageOutBuffer = 5,
    BasicInfo          = 0x40,
    Frame              = 0x400,
    FullImage          = 0x1000
}