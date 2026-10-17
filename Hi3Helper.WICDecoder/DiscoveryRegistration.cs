using System;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;
using Hi3Helper.WICDecoder.Decoder;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Enums.WIC;
using Hi3Helper.Win32.Native.Interfaces.WIC;
using Microsoft.Win32;

namespace Hi3Helper.WICDecoder;

public sealed class DiscoveryRegistration : IDisposable
{
    private const string DecoderCategory = "{7ED96837-96F0-4812-B211-F13C24117ED3}";
    private const string ClassesPath     = @"Software\Classes\CLSID";

    private readonly string _decoderPath;
    private readonly string _instancePath;
    private          bool   _registered;

    public DiscoveryRegistration(DecoderDescriptor descriptor)
    {
        string decoder = descriptor.Decoder.ToString("B").ToUpperInvariant();
        _decoderPath  = $@"{ClassesPath}\{decoder}";
        _instancePath = $@"{ClassesPath}\{DecoderCategory}\Instance\{decoder}";

        using RegistryKey root             = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        RegistryKey?      existingDecoder  = root.OpenSubKey(_decoderPath);
        RegistryKey?      existingInstance = root.OpenSubKey(_instancePath);
        if (existingDecoder is not null || existingInstance is not null)
        {
            existingDecoder?.Dispose();
            existingInstance?.Dispose();
            DeleteRegistration(root);

            existingInstance = null;
            existingDecoder  = null;
        }

        try
        {
            using (RegistryKey codec = root.CreateSubKey(_decoderPath, true))
            {
                codec.SetValue("FriendlyName",        $"Hi3Helper {descriptor.Name} Decoder", RegistryValueKind.String);
                codec.SetValue("Vendor",              IdsConst.Vendor.ToString("B"),          RegistryValueKind.String);
                codec.SetValue("ContainerFormat",     descriptor.Container.ToString("B"),     RegistryValueKind.String);
                codec.SetValue("MimeTypes",           descriptor.MimeTypes,                   RegistryValueKind.String);
                codec.SetValue("FileExtensions",      descriptor.FileExtensions,              RegistryValueKind.String);
                codec.SetValue("SupportAnimation",    descriptor.Animation ? 1 : 0,           RegistryValueKind.DWord);
                codec.SetValue("SupportChromaKey",    1,                                      RegistryValueKind.DWord);
                codec.SetValue("SupportLossless",     1,                                      RegistryValueKind.DWord);
                codec.SetValue("SupportMultiframe",   descriptor.Animation ? 1 : 0,           RegistryValueKind.DWord);
                codec.SetValue("ArbitrationPriority", 0,                                      RegistryValueKind.DWord);

                using (codec.CreateSubKey($@"Formats\{IdsConst.Bgra:B}", true))
                {
                }

                for (int index = 0; index < descriptor.Patterns.Length; index++)
                {
                    DecoderPattern    pattern = descriptor.Patterns[index];
                    using RegistryKey key     = codec.CreateSubKey($@"Patterns\{index}", true);
                    key.SetValue("Position",    checked((int)pattern.Position), RegistryValueKind.DWord);
                    key.SetValue("Length",      pattern.Pattern.Length,         RegistryValueKind.DWord);
                    key.SetValue("Pattern",     pattern.Pattern,                RegistryValueKind.Binary);
                    key.SetValue("Mask",        pattern.Mask,                   RegistryValueKind.Binary);
                    key.SetValue("EndOfStream", 0,                              RegistryValueKind.DWord);
                }
            }

            using (RegistryKey instance = root.CreateSubKey(_instancePath, true))
            {
                instance.SetValue("CLSID",        decoder,                                RegistryValueKind.String);
                instance.SetValue("FriendlyName", $"Hi3Helper {descriptor.Name} Decoder", RegistryValueKind.String);
            }

            _registered = true;
            RefreshWicComponents();
        }
        catch
        {
            DeleteRegistration(root);
            throw;
        }
        finally
        {
            existingDecoder?.Dispose();
            existingInstance?.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_registered) return;

        using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        DeleteRegistration(root);
        _registered = false;
        RefreshWicComponents();
    }

    private void DeleteRegistration(RegistryKey root)
    {
        root.DeleteSubKeyTree(_instancePath, false);
        root.DeleteSubKeyTree(_decoderPath, false);
    }

    private static void RefreshWicComponents()
    {
        if (!ComMarshal<IWICImagingFactory>.TryCreateComObject(in IdsConst.Factory,
                                                               CLSCTX.CLSCTX_INPROC_SERVER,
                                                               out IWICImagingFactory? factory,
                                                               out Exception? exception,
                                                               useUnique: true))
        {
            throw exception;
        }

        try
        {
            factory.CreateComponentEnumerator(WICComponentType.WICDecoder,
                                              WICComponentEnumerateOptions.WICComponentEnumerateRefresh,
                                              out nint enumerator).ThrowOnFailure();
            if (enumerator != 0) Marshal.Release(enumerator);
        }
        finally
        {
            ComMarshal<IWICImagingFactory>.FinalRelease(factory);
        }
    }
}
