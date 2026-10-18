using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Decoder;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using Hi3Helper.Win32.Native.Structs;

namespace Hi3Helper.WICDecoder;

[GeneratedComClass]
internal sealed partial class CodecClassFactory : IClassFactory
{
    private readonly DecoderDescriptor _descriptor;
    private readonly Guid              _registeredClsid;
    private          int               _locks;

    internal static int CreatedCount;

    internal CodecClassFactory(DecoderDescriptor descriptor, Guid registeredClsid)
    {
        _descriptor      = descriptor;
        _registeredClsid = registeredClsid;
    }

    public unsafe HResult CreateInstance(nint outer, in Guid iid, out nint instance)
    {
        instance = 0;
        if (outer != 0)
            return HResultConst.NoAggregation;

        Interlocked.Increment(ref CreatedCount);
        nint pointer = 0;
        try
        {
            IWICBitmapDecoder decoder = _descriptor.CreateDecoder(_registeredClsid);
            pointer = (nint)ComInterfaceMarshaller<IWICBitmapDecoder>.ConvertToUnmanaged(decoder);
            return Marshal.QueryInterface(pointer, in iid, out instance);
        }
        catch (Exception ex)
        {
            return ex.HResult;
        }
        finally
        {
            if (pointer != 0) Marshal.Release(pointer);
        }
    }

    public HResult LockServer(bool shouldLock)
    {
        // Host lifetime is explicit; this sample does not unload a COM DLL.
        if (shouldLock)
        {
            Interlocked.Increment(ref _locks);
            return 0;
        }

        while (true)
        {
            int count = Volatile.Read(ref _locks);
            if (count == 0)
            {
                return HResultConst.InvalidArg;
            }

            if (Interlocked.CompareExchange(ref _locks, count - 1, count) == count)
                return 0;
        }
    }
}