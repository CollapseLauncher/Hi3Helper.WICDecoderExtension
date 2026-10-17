namespace Hi3Helper.WICDecoder.Decoder.Webp;

internal readonly record struct WebpInfo(uint  Width,
                                         uint  Height,
                                         uint  FrameCount,
                                         uint  LoopCount,
                                         uint  BackgroundColor,
                                         uint  FormatFlags,
                                         ulong CanvasBytes);