using System;

namespace Hi3Helper.WICDecoder.Decoder;

internal sealed record DecodedImage(ImageFrame?[] Frames,
                                    bool          Animated,
                                    uint          LoopCount,
                                    uint          BackgroundBgra) : IDisposable
{
    public void Dispose()
    {
        foreach (ImageFrame? frame in Frames)
        {
            frame?.Dispose();
        }
    }
}
