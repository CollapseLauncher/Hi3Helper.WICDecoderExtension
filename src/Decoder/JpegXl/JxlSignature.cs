namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

internal enum JxlSignature
{
    NotEnoughBytes = 0,
    Invalid        = 1,
    Codestream     = 2,
    Container      = 3
}