using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.WICDecoder;

internal static unsafe class BufferHelper
{
    private static readonly Vector128<byte> RgbaToBgra8Mask128 = Vector128.Create((byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);
    private static readonly Vector256<byte> RgbaToBgra8Mask256 = Vector256.Create(RgbaToBgra8Mask128, RgbaToBgra8Mask128);
    private static readonly Vector512<byte> RgbaToBgra8Mask512 = Vector512.Create(RgbaToBgra8Mask256, RgbaToBgra8Mask256);
    private static readonly Vector128<byte> AlphaMask128 = Vector128.Create(0xFF000000u).AsByte();
    private static readonly Vector256<byte> AlphaMask256 = Vector256.Create(0xFF000000u).AsByte();
    private static readonly Vector512<byte> AlphaMask512 = Vector512.Create(0xFF000000u).AsByte();

    private static readonly byte[] UnpremultiplyTable    = CreateUnpremultiplyTable();
    private static readonly uint[] UnpremultiplyTable32  = CreateUnpremultiplyTable32();
    private static readonly uint*  UnpremultiplyTable32P = (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(UnpremultiplyTable32, 0);

    /// <summary>
    /// Reverse position of the first to third channel while ignoring the forth position (Alpha) unchanged.
    /// </summary>
    /// <param name="pixels">The pixels buffer to reverse</param>
    public static void ReverseXxxa(Span<byte> pixels)
    {
        ShuffleMask4(pixels,
                     in RgbaToBgra8Mask128,
                     in RgbaToBgra8Mask256,
                     in RgbaToBgra8Mask512);
    }

    private static void ShuffleMask4(Span<byte>                   span,
                                     ref readonly Vector128<byte> mask128,
                                     ref readonly Vector256<byte> mask256,
                                     ref readonly Vector512<byte> mask512)
    {
        if ((span.Length & 3) != 0)
            throw new ArgumentException("Buffer length must be divisible by 4.", nameof(span));

        ref byte start = ref MemoryMarshal.GetReference(span);
        ref byte current = ref start;

        int length = span.Length;

        // -- AVX512BW: 512-bit
        if (Avx512BW.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref start, length & ~63);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector512<byte>>(ref current);
                value = Avx512BW.Shuffle(value, mask512);

                Unsafe.WriteUnaligned(ref current, value);
                current = ref Unsafe.Add(ref current, 64);
            }
        }

        // -- AVX2: 256-bit
        if (Avx2.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref start, length & ~31);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector256<byte>>(ref current);
                value = Avx2.Shuffle(value, mask256);

                Unsafe.WriteUnaligned(ref current, value);
                current = ref Unsafe.Add(ref current, 32);
            }
        }

        // -- AVX2: 128-bit
        if (Ssse3.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref start, length & ~15);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector128<byte>>(ref current);
                value = Ssse3.Shuffle(value, mask128);

                Unsafe.WriteUnaligned(ref current, value);
                current = ref Unsafe.Add(ref current, 16);
            }
        }

        // -- Scalar: 1 pixel
        ref byte scalarEnd = ref Unsafe.Add(ref start, length);
        while (Unsafe.IsAddressLessThan(ref current, ref scalarEnd))
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref current);
            source = (source & 0xFF00FF00u) | ((source & 0x00FF0000u) >> 16) | ((source & 0x000000FFu) << 16);

            current = ref Unsafe.Add(ref current, 4);
        }
    }

    public static unsafe void CopyPlaneToFrame4(byte*      source,
                                                int        sourceStride,
                                                int        width,
                                                int        height,
                                                bool       hasAlpha,
                                                bool       isPremultiplied,
                                                Span<byte> destination)
    {
        if (width < 0 || height < 0 ||
            (long)width * height * 4 > destination.Length)
            throw new ArgumentException("Invalid image dimensions.");

        int rowBytes = checked(width * 4);

        // Without an alpha channel, the image is fully opaque.
        bool unpremultiply = hasAlpha && isPremultiplied;

        for (int y = 0; y < height; y++)
        {
            byte*      sourceRow      = source + (long)y * sourceStride;
            Span<byte> destinationRow = destination.Slice(y * rowBytes, rowBytes);

            if (unpremultiply)
            {
                CopyPremultipliedRow(sourceRow, destinationRow, width);
            }
            else
            {
                CopyPixelRowSimd4(sourceRow,
                                  destinationRow,
                                  width,
                                  hasAlpha,
                                  in RgbaToBgra8Mask128,
                                  in AlphaMask128,
                                  in RgbaToBgra8Mask256,
                                  in AlphaMask256,
                                  in RgbaToBgra8Mask512,
                                  in AlphaMask512);
            }
        }
    }

    private static unsafe void CopyPixelRowSimd4(byte*                        source,
                                                 Span<byte>                   destination,
                                                 int                          width,
                                                 bool                         hasAlpha,
                                                 ref readonly Vector128<byte> mask128,
                                                 ref readonly Vector128<byte> alphaMask128,
                                                 ref readonly Vector256<byte> mask256,
                                                 ref readonly Vector256<byte> alphaMask256,
                                                 ref readonly Vector512<byte> mask512,
                                                 ref readonly Vector512<byte> alphaMask512)
    {
        ref byte srcStart = ref Unsafe.AsRef<byte>(source);
        ref byte dstStart = ref MemoryMarshal.GetReference(destination);
        ref byte src      = ref srcStart;
        ref byte dst      = ref dstStart;

        int length = width * 4;

        // -- AVX512BW: 512-bit
        if (Avx512BW.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref srcStart, length & ~63);
            while (Unsafe.IsAddressLessThan(ref src, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector512<byte>>(ref src);
                value = Avx512BW.Shuffle(value, mask512);

                if (!hasAlpha)
                    value |= alphaMask512;

                Unsafe.WriteUnaligned(ref dst, value);

                src = ref Unsafe.Add(ref src, 64);
                dst = ref Unsafe.Add(ref dst, 64);
            }
        }

        // -- AVX2: 256-bit
        if (Avx2.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref srcStart, length & ~31);
            while (Unsafe.IsAddressLessThan(ref src, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector256<byte>>(ref src);
                value = Avx2.Shuffle(value, mask256);

                if (!hasAlpha)
                    value |= alphaMask256;

                Unsafe.WriteUnaligned(ref dst, value);

                src = ref Unsafe.Add(ref src, 32);
                dst = ref Unsafe.Add(ref dst, 32);
            }
        }

        // -- AVX2: 128-bit
        if (Ssse3.IsSupported)
        {
            ref byte end = ref Unsafe.Add(ref srcStart, length & ~15);
            while (Unsafe.IsAddressLessThan(ref src, ref end))
            {
                var value = Unsafe.ReadUnaligned<Vector128<byte>>(ref src);
                value = Ssse3.Shuffle(value, mask128);

                if (!hasAlpha)
                    value |= alphaMask128;

                Unsafe.WriteUnaligned(ref dst, value);

                src = ref Unsafe.Add(ref src, 16);
                dst = ref Unsafe.Add(ref dst, 16);
            }
        }

        // -- Scalar: 1 pixel
        ref byte scalarEnd = ref Unsafe.Add(ref srcStart, length);
        while (Unsafe.IsAddressLessThan(ref src, ref scalarEnd))
        {
            dst = Unsafe.Add(ref src, 2);
            Unsafe.Add(ref dst,       1) = Unsafe.Add(ref src, 1);
            Unsafe.Add(ref dst,       2) = src;
            Unsafe.Add(ref dst,       3) = hasAlpha ? Unsafe.Add(ref src, 3) : (byte)255;

            src = ref Unsafe.Add(ref src, 4);
            dst = ref Unsafe.Add(ref dst, 4);
        }
    }

    private static unsafe void CopyPremultipliedRow(byte*      source,
                                                    Span<byte> destination,
                                                    int        width)
    {
        if (width < 0 || (long)width * 4 > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(width));

        ref byte srcStart = ref Unsafe.AsRef<byte>(source);
        ref byte src      = ref srcStart;
        ref byte dst      = ref MemoryMarshal.GetReference(destination);
        ref byte lut      = ref MemoryMarshal.GetArrayDataReference(UnpremultiplyTable);

        int length = width * 4;

        // -- AVX2: 256-bit
        if (Avx2.IsSupported && length >= 32)
        {
            var      byteMask = Vector256.Create(255u);
            ref byte end      = ref Unsafe.Add(ref srcStart, length & ~31);
            while (Unsafe.IsAddressLessThan(ref src, ref end))
            {
                // Load 8 RGBA pixels (32 bytes).
                var pixels = Unsafe.ReadUnaligned<Vector256<uint>>(ref src);

                Vector256<uint> r = Avx2.And(pixels,                             byteMask);
                Vector256<uint> g = Avx2.And(Avx2.ShiftRightLogical(pixels, 8),  byteMask);
                Vector256<uint> b = Avx2.And(Avx2.ShiftRightLogical(pixels, 16), byteMask);
                Vector256<uint> a = Avx2.ShiftRightLogical(pixels, 24);

                // Construct LUT indices: (alpha << 8) | channel.
                Vector256<uint> alphaIndex = Avx2.ShiftLeftLogical(a, 8);

                Vector256<uint> ri = Avx2.Or(alphaIndex, r);
                Vector256<uint> gi = Avx2.Or(alphaIndex, g);
                Vector256<uint> bi = Avx2.Or(alphaIndex, b);

                // 8 parallel lookups per channel.
                Vector256<uint> rr = Avx2.GatherVector256(UnpremultiplyTable32P, ri.AsInt32(), 4);
                Vector256<uint> gg = Avx2.GatherVector256(UnpremultiplyTable32P, gi.AsInt32(), 4);
                Vector256<uint> bb = Avx2.GatherVector256(UnpremultiplyTable32P, bi.AsInt32(), 4);

                // Reassemble into BGRA.
                Vector256<uint> result = Avx2.Or(Avx2.Or(bb, Avx2.ShiftLeftLogical(gg, 8)),
                                                 Avx2.Or(Avx2.ShiftLeftLogical(rr, 16), Avx2.ShiftLeftLogical(a, 24)));

                Unsafe.WriteUnaligned(ref dst, result);

                src = ref Unsafe.Add(ref src, 32);
                dst = ref Unsafe.Add(ref dst, 32);
            }
        }

        // Original scalar LUT fallback and SIMD remainder.
        ref byte scalarEnd = ref Unsafe.Add(ref srcStart, length);
        while (Unsafe.IsAddressLessThan(ref src, ref scalarEnd))
        {
            byte alpha = Unsafe.Add(ref src, 3);
            int  index = alpha << 8;

            dst = Unsafe.Add(ref lut, index | Unsafe.Add(ref src, 2));

            Unsafe.Add(ref dst,       1) = Unsafe.Add(ref lut, index | Unsafe.Add(ref src, 1));
            Unsafe.Add(ref dst,       2) = Unsafe.Add(ref lut, index | src);
            Unsafe.Add(ref dst,       3) = alpha;

            src = ref Unsafe.Add(ref src, 4);
            dst = ref Unsafe.Add(ref dst, 4);
        }
    }

    private static byte[] CreateUnpremultiplyTable()
    {
        byte[] table = new byte[256 * 256];

        for (int alpha = 1; alpha < 256; alpha++)
        {
            for (int value = 0; value < 256; value++)
            {
                table[(alpha << 8) | value] = (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
            }
        }

        return table;
    }

    private static uint[] CreateUnpremultiplyTable32()
    {
        uint[] table = new uint[256 * 256];

        for (int i = 0; i < table.Length; i++)
            table[i] = UnpremultiplyTable[i];

        return table;
    }
}