using System;
using Hi3Helper.WICDecoder.Decoder;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using Hi3Helper.Win32.Native.LibraryImport;

namespace Hi3Helper.WICDecoder;

public sealed class RuntimeRegistration : IDisposable
{
    private readonly uint _cookie;
    private          bool _registered;
    private readonly int  _threadId = Environment.CurrentManagedThreadId;

    public RuntimeRegistration(DecoderDescriptor descriptor)
        : this(descriptor, in descriptor.Decoder)
    {
    }

    public RuntimeRegistration(DecoderDescriptor descriptor, in Guid registeredClsid)
    {
        CodecClassFactory factory = new(descriptor, registeredClsid);
        PInvoke.CoRegisterClassObject(in registeredClsid,
                                      factory,
                                      CLSCTX.CLSCTX_LOCAL_SERVER,
                                      TagREGCLS.REGCLS_MULTIPLEUSE,
                                      out _cookie).ThrowOnFailure();
        _registered = true;

        try
        {
            VerifyInProcessActivation(descriptor.Name, in registeredClsid);
        }
        catch
        {
            RevokeClassObject();
            throw;
        }
    }

    public void Dispose()
    {
        if (!_registered) return;

        if (_threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Revoke on the registration thread before COM shutdown.");

        RevokeClassObject();
    }

    private static void VerifyInProcessActivation(string  name,
                                                  in Guid registeredClsid)
    {
        if (!ComMarshal<IWICBitmapDecoder>.TryCreateComObject(in registeredClsid,
                                                              CLSCTX.CLSCTX_INPROC_SERVER,
                                                              out IWICBitmapDecoder? decoder,
                                                              out Exception? activationException,
                                                              useUnique: true))
        {
            throw new InvalidOperationException($"The process-local {name} class factory could not be activated.", activationException);
        }

        try
        {
            Guid interfaceId = typeof(IAnimationInfo).GUID;
            if (!ComMarshal<IWICBitmapDecoder>.TryCastComObjectAs(decoder,
                                                                  in interfaceId,
                                                                  out IAnimationInfo? animationInfo,
                                                                  out Exception? interfaceException,
                                                                  useUnique: true))
            {
                throw new InvalidOperationException($"COM activated a different {name} decoder for {registeredClsid:B}.", interfaceException);
            }

            ComMarshal<IAnimationInfo>.FinalRelease(animationInfo);
        }
        finally
        {
            ComMarshal<IWICBitmapDecoder>.FinalRelease(decoder);
        }
    }

    private void RevokeClassObject()
    {
        PInvoke.CoRevokeClassObject(_cookie).ThrowOnFailure();
        _registered = false;
    }
}