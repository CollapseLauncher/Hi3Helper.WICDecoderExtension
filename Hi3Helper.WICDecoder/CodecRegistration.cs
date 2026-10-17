using Hi3Helper.WICDecoder.Decoder;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Hi3Helper.WICDecoder;

public static class CodecRegistration
{
    private static readonly Dictionary<DecoderDescriptor, Registration> LoadedCodec     = [];
    private static readonly Lock                                        LoadedCodecLock = new();

    private record Registration(RuntimeRegistration   Runtime,
                                DiscoveryRegistration Discovery);


    public static void RegisterOrGetCustomCodecs(DecoderDescriptor         descriptor,
                                                 out RuntimeRegistration   runtime,
                                                 out DiscoveryRegistration discovery)
    {
        Unsafe.SkipInit(out runtime);
        Unsafe.SkipInit(out discovery);

        using (LoadedCodecLock.EnterScope())
        {
            if (LoadedCodec.TryGetValue(descriptor, out Registration? registration))
            {
                runtime   = registration.Runtime;
                discovery = registration.Discovery;
                return;
            }

            runtime   = new RuntimeRegistration(descriptor);
            discovery = new DiscoveryRegistration(descriptor);
            LoadedCodec.Add(descriptor, new Registration(runtime, discovery));
        }
    }

    public static void GetRegisteredCustomCodecs(out DecoderDescriptor[] registeredDescriptors)
    {
        using (LoadedCodecLock.EnterScope())
        {
            registeredDescriptors = LoadedCodec.Keys.Count == 0 ? [] : [.. LoadedCodec.Keys];
        }
    }

    public static void UnregisterCustomCodecs(DecoderDescriptor codecGuid)
    {
        using (LoadedCodecLock.EnterScope())
        {
            if (!LoadedCodec.Remove(codecGuid, out Registration? registration))
                return;

            registration.Discovery.Dispose();
            registration.Runtime.Dispose();
        }
    }
}