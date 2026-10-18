using System;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.Win32.Native.Interfaces;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder.Decoder.JpegXl;

internal static class JpegXlBackend
{
    private const int MaximumFrames     = 4096;
    private const int MaximumPixelBytes = 256 * 1024 * 1024;

    private const JxlDecoderStatus RequestedEvents = JxlDecoderStatus.BasicInfo |
                                                     JxlDecoderStatus.Frame |
                                                     JxlDecoderStatus.FullImage;

    private static readonly JxlPixelFormat PixelFormat = new(4, JxlDataType.UInt8, JxlEndianness.Native, 0);

    public static DecodedImage Decode(IStream stream)
    {
        using EncodedInput input = EncodedInput.Read(stream);
        ValidateSignature(input);

        nint decoder = CreateDecoder(input);
        try
        {
            using JpegXlSequence sequence = new(MaximumFrames, MaximumPixelBytes);
            ProcessInput(decoder, sequence);
            return sequence.DetachImage();
        }
        finally
        {
            JpegXlInterop.JxlDecoderDestroy(decoder);
        }
    }

    private static void ValidateSignature(EncodedInput input)
    {
        JxlSignature signature = JpegXlInterop.JxlSignatureCheck(input.Buffer, input.Length);
        if (signature is not JxlSignature.Codestream and not JxlSignature.Container)
        {
            throw new COMException("The input does not contain a JPEG XL signature.", HResultConst.BadImage);
        }
    }

    private static nint CreateDecoder(EncodedInput input)
    {
        nint decoder = JpegXlInterop.JxlDecoderCreate(nint.Zero);
        if (decoder == 0)
        {
            throw new OutOfMemoryException("libjxl could not allocate a decoder.");
        }

        try
        {
            CheckStatus(JpegXlInterop.JxlDecoderSetCoalescing(decoder, 1), "enable composited frame output");
            CheckStatus(JpegXlInterop.JxlDecoderSetUnpremultiplyAlpha(decoder, 1), "request straight alpha");
            CheckStatus(JpegXlInterop.JxlDecoderSubscribeEvents(decoder, RequestedEvents), "subscribe to decoder events");
            CheckStatus(JpegXlInterop.JxlDecoderSetInput(decoder, input.Buffer, input.Length), "supply the encoded input");

            JpegXlInterop.JxlDecoderCloseInput(decoder);
            return decoder;
        }
        catch
        {
            JpegXlInterop.JxlDecoderDestroy(decoder);
            throw;
        }
    }

    private static void ProcessInput(nint           decoder,
                                     JpegXlSequence sequence)
    {
        while (true)
        {
            JxlDecoderStatus status = JpegXlInterop.JxlDecoderProcessInput(decoder);
            switch (status)
            {
                case JxlDecoderStatus.BasicInfo:
                    ReadBasicInformation(decoder, sequence);
                    break;

                case JxlDecoderStatus.Frame:
                    ReadFrameHeader(decoder, sequence);
                    break;

                case JxlDecoderStatus.NeedImageOutBuffer:
                    SetFrameBuffer(decoder, sequence);
                    break;

                case JxlDecoderStatus.FullImage:
                    sequence.CompleteFrame();
                    break;

                case JxlDecoderStatus.Success:
                    sequence.ValidateComplete();
                    return;

                case JxlDecoderStatus.NeedMoreInput:
                    throw new COMException("The JPEG XL stream ended before decoding completed.", HResultConst.BadImage);

                default:
                    throw new COMException($"libjxl stopped with decoder status {(int)status}.", HResultConst.BadImage);
            }
        }
    }

    private static void ReadBasicInformation(nint           decoder,
                                             JpegXlSequence sequence)
    {
        CheckStatus(JpegXlInterop.JxlDecoderGetBasicInfo(decoder, out JxlBasicInfo information), "read the image header");
        sequence.SetBasicInformation(in information);
    }

    private static void ReadFrameHeader(nint           decoder,
                                        JpegXlSequence sequence)
    {
        CheckStatus(JpegXlInterop.JxlDecoderGetFrameHeader(decoder, out JxlFrameHeader header), "read a frame header");

        sequence.BeginFrame(in header);
    }

    private static void SetFrameBuffer(nint           decoder,
                                       JpegXlSequence sequence)
    {
        CheckStatus(JpegXlInterop.JxlDecoderImageOutBufferSize(decoder, in PixelFormat, out nuint requiredSize), "measure the frame buffer");
        sequence.AllocateFrame(requiredSize);
        
        CheckStatus(JpegXlInterop.JxlDecoderSetImageOutBuffer(decoder, in PixelFormat, sequence.FrameBuffer, requiredSize), "set the frame buffer");
    }

    private static void CheckStatus(JxlDecoderStatus status,
                                    string           operation)
    {
        if (status != JxlDecoderStatus.Success)
        {
            throw new COMException($"libjxl could not {operation} (status {(int)status}).", HResultConst.BadImage);
        }
    }
}
