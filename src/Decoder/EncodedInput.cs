using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.Win32.Native.Interfaces;

namespace Hi3Helper.WICDecoder.Decoder;

internal sealed unsafe class EncodedInput : IDisposable
{
    public const  int MaxEncodedBytes = 128 * 1024 * 1024;
    private const int InitialCapacity = 64 * 1024;

    private nint _buffer;

    private EncodedInput(nint buffer, uint length)
    {
        _buffer = buffer;
        Length  = length;
    }

    public uint               Length { get; }
    public nint               Buffer => _buffer;
    public ReadOnlySpan<byte> Span   => new((void*)_buffer, (int)Length);

    public static EncodedInput Read(IStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin, out _);
        nuint capacity = InitialCapacity;
        byte* buffer   = (byte*)NativeMemory.Alloc(capacity);

        if (buffer == null)
        {
            throw new OutOfMemoryException();
        }

        uint length = 0;
        try
        {
            while (true)
            {
                if (length == (int)capacity)
                {
                    if (capacity >= MaxEncodedBytes)
                    {
                        byte overflow;
                        stream.Read((nint)(&overflow), 1, out uint overflowRead);

                        if (overflowRead == 0)
                            break;

                        if (overflowRead > 1)
                            throw new COMException("Invalid stream read count.", HResultConst.BadImage);

                        throw new COMException("Encoded image exceeds the sample limit.", HResultConst.BadImage);
                    }

                    nuint newCapacity = Math.Min(capacity * 2, MaxEncodedBytes);
                    byte* resized     = (byte*)NativeMemory.Realloc(buffer, newCapacity);
                    if (resized == null) throw new OutOfMemoryException();
                    buffer   = resized;
                    capacity = newCapacity;
                }

                uint available = checked((uint)(capacity - length));
                stream.Read((nint)(buffer + length), available, out uint read);
                if (read > available)
                {
                    throw new COMException("Invalid stream read count.", HResultConst.BadImage);
                }

                if (read == 0)
                {
                    break;
                }

                length = checked(length + read);
            }

            return new EncodedInput((nint)buffer, length);
        }
        catch
        {
            NativeMemory.Free(buffer);
            throw;
        }
    }

    public void Dispose()
    {
        nint buffer;
        if ((buffer = Interlocked.Exchange(ref _buffer, nint.Zero)) != nint.Zero)
        {
            NativeMemory.Free((void*)buffer);
        }

        GC.SuppressFinalize(this);
    }

    ~EncodedInput() => Dispose();
}
