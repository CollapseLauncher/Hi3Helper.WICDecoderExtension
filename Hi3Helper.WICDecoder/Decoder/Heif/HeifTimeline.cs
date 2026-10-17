using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hi3Helper.WICDecoder.Const;


// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.WICDecoder.Decoder.Heif;

// Parse only timeline metadata; libheif still decodes all compressed samples.
// Looping uses tkhd duration / elst segment duration (both in movie units), not
// mvhd duration / media duration. Those differ in ordinary FFmpeg-produced files.
internal sealed record HeifTimeline(uint   Timescale,
                                    uint   LoopCount,
                                    uint[] Durations)
{
    private readonly record struct Box(uint Type, int Offset, int Length);

    private readonly record struct MediaTimeline(uint Timescale, ulong Duration, uint[] Durations);

    private readonly record struct EditListEntry(ulong SegmentDuration, ulong MediaStart, uint Rate, uint Flags)
    {
        public bool Repeats => (Flags & 1) != 0;
    }

    private const uint Moov = 0x6D6F6F76;
    private const uint Mvhd = 0x6D766864;
    private const uint Trak = 0x7472616B;
    private const uint Tkhd = 0x746B6864;
    private const uint Mdia = 0x6D646961;
    private const uint Mdhd = 0x6D646864;
    private const uint Minf = 0x6D696E66;
    private const uint Stbl = 0x7374626C;
    private const uint Stts = 0x73747473;
    private const uint Ctts = 0x63747473;
    private const uint Edts = 0x65647473;
    private const uint Elst = 0x656C7374;

    internal static HeifTimeline Read(ReadOnlySpan<byte> encoded, uint trackId)
    {
        ReadOnlySpan<byte> movie          = GetRequiredPayload(encoded, Moov);
        uint               movieTimescale = ReadMovieTimescale(movie);

        ReadOnlySpan<byte> track         = FindTrack(movie, trackId);
        ulong              trackDuration = ReadTrackDuration(track);
        MediaTimeline      media         = ReadMediaTimeline(track);

        // Validate that converting the complete duration to milliseconds is safe.
        _ = HeifBackend.Milliseconds(media.Duration, media.Timescale);
        uint loopCount = ReadLoopCount(track, trackDuration, movieTimescale, media);

        return new HeifTimeline(media.Timescale, loopCount, media.Durations);
    }

    private static uint ReadMovieTimescale(ReadOnlySpan<byte> movie)
    {
        ReadOnlySpan<byte> movieHeader = GetRequiredPayload(movie, Mvhd);
        uint               timescale   = ReadUInt32(movieHeader, GetTimeFieldOffset(movieHeader));

        if (timescale == 0)
        {
            throw CreateBadImageException();
        }

        return timescale;
    }

    private static ReadOnlySpan<byte> FindTrack(ReadOnlySpan<byte> movie, uint expectedTrackId)
    {
        Box  selectedTrack    = default;
        bool hasSelectedTrack = false;
        int  offset           = 0;

        while (TryReadNextBox(movie, ref offset, out Box track))
        {
            if (track.Type != Trak)
            {
                continue;
            }

            ReadOnlySpan<byte> trackData = GetPayload(movie, track);
            uint               trackId   = ReadTrackId(trackData);

            if (trackId != expectedTrackId)
            {
                continue;
            }

            if (hasSelectedTrack)
            {
                throw CreateBadImageException();
            }

            selectedTrack     = track;
            hasSelectedTrack  = true;
        }

        if (!hasSelectedTrack)
        {
            throw CreateBadImageException();
        }

        return GetPayload(movie, selectedTrack);
    }

    private static uint ReadTrackId(ReadOnlySpan<byte> track)
    {
        ReadOnlySpan<byte> trackHeader = GetRequiredPayload(track, Tkhd);
        return ReadUInt32(trackHeader, GetTimeFieldOffset(trackHeader));
    }

    private static ulong ReadTrackDuration(ReadOnlySpan<byte> track)
    {
        ReadOnlySpan<byte> trackHeader     = GetRequiredPayload(track, Tkhd);
        int                timeFieldOffset = GetTimeFieldOffset(trackHeader);
        return ReadDuration(trackHeader, timeFieldOffset + 8);
    }

    private static MediaTimeline ReadMediaTimeline(ReadOnlySpan<byte> track)
    {
        ReadOnlySpan<byte> media       = GetRequiredPayload(track, Mdia);
        uint               timescale   = ReadMediaTimescale(media);
        ReadOnlySpan<byte> sampleTable = GetSampleTable(media);
        uint[]             durations   = ReadSampleDurations(sampleTable, out ulong totalDuration);

        ValidateCompositionOffsets(sampleTable);
        return new MediaTimeline(timescale, totalDuration, durations);
    }

    private static uint ReadMediaTimescale(ReadOnlySpan<byte> media)
    {
        ReadOnlySpan<byte> mediaHeader = GetRequiredPayload(media, Mdhd);
        uint               timescale   = ReadUInt32(mediaHeader, GetTimeFieldOffset(mediaHeader));

        if (timescale == 0)
        {
            throw CreateBadImageException();
        }

        return timescale;
    }

    private static ReadOnlySpan<byte> GetSampleTable(ReadOnlySpan<byte> media)
    {
        ReadOnlySpan<byte> mediaInformation = GetRequiredPayload(media, Minf);
        return GetRequiredPayload(mediaInformation, Stbl);
    }

    private static uint[] ReadSampleDurations(ReadOnlySpan<byte> sampleTable,
                                              out ulong totalDuration)
    {
        ReadOnlySpan<byte> timeToSample = GetRequiredPayload(sampleTable, Stts);
        ValidateTimeToSampleHeader(timeToSample, out uint entryCount);

        List<uint> durations = [];
        totalDuration = 0;

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            int  entryOffset = 8 + entryIndex * 8;
            uint sampleCount = ReadUInt32(timeToSample, entryOffset);
            uint duration    = ReadUInt32(timeToSample, entryOffset + 4);

            ValidateSampleCount(sampleCount, durations.Count);
            AddSampleDurations(durations, sampleCount, duration);
            totalDuration += (ulong)sampleCount * duration;
        }

        if (durations.Count == 0 || totalDuration == 0)
        {
            throw CreateBadImageException();
        }

        return [.. durations];
    }

    private static void ValidateTimeToSampleHeader(ReadOnlySpan<byte> timeToSample,
                                                   out uint           entryCount)
    {
        uint versionAndFlags = ReadUInt32(timeToSample, 0);
        entryCount           = ReadUInt32(timeToSample, 4);
        ulong expectedLength = (ulong)entryCount * 8 + 8;

        if (versionAndFlags != 0 ||
            entryCount > 4096 ||
            expectedLength != (ulong)timeToSample.Length)
        {
            throw CreateBadImageException();
        }
    }

    private static void ValidateSampleCount(uint sampleCount, int currentSampleCount)
    {
        if (sampleCount == 0 ||
            sampleCount > 4096 - currentSampleCount)
        {
            throw CreateBadImageException();
        }
    }

    private static void AddSampleDurations(List<uint> durations,
                                           uint       sampleCount,
                                           uint       duration)
    {
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            durations.Add(duration);
        }
    }

    private static void ValidateCompositionOffsets(ReadOnlySpan<byte> sampleTable)
    {
        if (!TryGetOptionalBox(sampleTable, Ctts, out Box compositionOffsetBox))
        {
            return;
        }

        ReadOnlySpan<byte> compositionOffsets = GetPayload(sampleTable, compositionOffsetBox);
        uint               entryCount         = ReadUInt32(compositionOffsets, 4);
        ulong              expectedLength     = (ulong)entryCount * 8 + 8;

        if (entryCount > 4096 ||
            expectedLength != (ulong)compositionOffsets.Length)
        {
            throw CreateBadImageException();
        }

        // The libheif sequence API does not expose presentation timestamps.
        // This sample therefore accepts presentation order == decode order only.
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            uint compositionOffset = ReadUInt32(compositionOffsets, 12 + entryIndex * 8);
            if (compositionOffset != 0)
            {
                throw CreateUnsupportedEditException();
            }
        }
    }

    private static uint ReadLoopCount(ReadOnlySpan<byte> track,
                                      ulong              trackDuration,
                                      uint               movieTimescale,
                                      MediaTimeline      media)
    {
        if (!TryGetOptionalBox(track, Edts, out Box editsBox))
        {
            return 1;
        }

        ReadOnlySpan<byte> edits    = GetPayload(track, editsBox);
        ReadOnlySpan<byte> editList = GetRequiredPayload(edits, Elst);
        EditListEntry      entry    = ReadEditListEntry(editList);

        ValidateEditListEntry(entry, movieTimescale, media);
        if (!entry.Repeats)
        {
            return 1;
        }

        return CalculateLoopCount(trackDuration,
                                  entry.SegmentDuration);
    }

    private static EditListEntry ReadEditListEntry(ReadOnlySpan<byte> editList)
    {
        if (ReadUInt32(editList, 4) != 1)
        {
            throw CreateUnsupportedEditException();
        }

        if (editList.IsEmpty || editList[0] > 1)
        {
            throw CreateBadImageException();
        }

        bool isWide = editList[0] == 1;
        int expectedLength = isWide ? 28 : 20;
        if (editList.Length != expectedLength)
        {
            throw CreateBadImageException();
        }

        ulong segmentDuration = isWide ? ReadUInt64(editList, 8) : ReadUInt32(editList,  8);
        ulong mediaStart      = isWide ? ReadUInt64(editList, 16) : ReadUInt32(editList, 12);
        uint  rate            = ReadUInt32(editList, isWide ? 24 : 16);
        uint  flags           = ReadUInt32(editList, 0) & 0xFFFFFF;

        return new EditListEntry(segmentDuration, mediaStart, rate, flags);
    }

    private static void ValidateEditListEntry(EditListEntry entry,
                                              uint          movieTimescale,
                                              MediaTimeline media)
    {
        if (entry.MediaStart != 0 ||
            entry.Rate != 0x10000 ||
            entry.Flags > 1)
        {
            throw CreateUnsupportedEditException();
        }

        // A complete media cycle can round down or up in the movie timescale.
        UInt128 exactDuration   = (UInt128)media.Duration * movieTimescale;
        ulong   floorDuration   = (ulong)(exactDuration / media.Timescale);
        ulong   ceilingDuration = (ulong)((exactDuration + media.Timescale - 1) / media.Timescale);

        if (entry.SegmentDuration == 0 ||
            (entry.SegmentDuration != floorDuration &&
             entry.SegmentDuration != ceilingDuration))
        {
            throw CreateUnsupportedEditException();
        }
    }

    private static uint CalculateLoopCount(ulong trackDuration,
                                           ulong segmentDuration)
    {
        switch (trackDuration)
        {
            // FFmpeg writes INT64_MAX for infinite HEIF-family; ISO BMFF uses all bits set.
            case ulong.MaxValue or long.MaxValue:
                return 0;
            case 0:
                throw CreateBadImageException();
        }

        ulong completeLoops = trackDuration / segmentDuration;
        ulong partialLoop   = trackDuration % segmentDuration == 0 ? 0UL : 1UL;
        ulong loopCount     = completeLoops + partialLoop;

        if (loopCount > uint.MaxValue)
        {
            throw CreateUnsupportedEditException();
        }

        return (uint)loopCount;
    }

    private static int GetTimeFieldOffset(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data[0] > 1)
        {
            throw CreateBadImageException();
        }

        return data[0] == 1 ? 20 : 12;
    }

    private static ulong ReadDuration(ReadOnlySpan<byte> data, int offset)
    {
        if (data[0] == 1)
        {
            return ReadUInt64(data, offset);
        }

        uint value = ReadUInt32(data, offset);
        return value == uint.MaxValue
            ? ulong.MaxValue
            : value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || data.Length - offset < 4)
        {
            throw CreateBadImageException();
        }

        return BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || data.Length - offset < 8)
        {
            throw CreateBadImageException();
        }

        return BinaryPrimitives.ReadUInt64BigEndian(data[offset..]);
    }

    private static ReadOnlySpan<byte> GetRequiredPayload(ReadOnlySpan<byte> data, uint type)
    {
        if (!TryGetOptionalBox(data, type, out Box box))
        {
            throw CreateBadImageException();
        }

        return GetPayload(data, box);
    }

    private static ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> data, Box box)
    {
        return data.Slice(box.Offset, box.Length);
    }

    private static bool TryGetOptionalBox(ReadOnlySpan<byte> data,
                                          uint               type,
                                          out Box            result)
    {
        result = default;
        bool found  = false;
        int  offset = 0;

        while (TryReadNextBox(data, ref offset, out Box box))
        {
            if (box.Type != type)
            {
                continue;
            }

            if (found)
            {
                throw CreateBadImageException();
            }

            result = box;
            found  = true;
        }

        return found;
    }

    private static bool TryReadNextBox(ReadOnlySpan<byte> data,
                                       ref int            offset,
                                       out Box            box)
    {
        if ((uint)offset > (uint)data.Length)
        {
            throw CreateBadImageException();
        }

        if (offset == data.Length)
        {
            box = default;
            return false;
        }

        ReadOnlySpan<byte> remaining = data[offset..];
        if (remaining.Length < 8)
        {
            throw CreateBadImageException();
        }

        ulong size       = ReadUInt32(remaining, 0);
        int   headerSize = 8;

        switch (size)
        {
            case 1 when remaining.Length < 16:
                throw CreateBadImageException();
            case 1:
                size       = ReadUInt64(remaining, 8);
                headerSize = 16;
                break;
            case 0:
                size = (ulong)remaining.Length;
                break;
        }

        if (size < (uint)headerSize ||
            size > (ulong)remaining.Length ||
            size > int.MaxValue)
        {
            throw CreateBadImageException();
        }

        int boxLength = (int)size;
        box    = new Box(ReadUInt32(remaining, 4), checked(offset + headerSize), boxLength - headerSize);
        offset = checked(offset + boxLength);
        return true;
    }

    private static COMException CreateBadImageException()
    {
        return new COMException("Invalid HEIF-family timeline metadata.", HResultConst.BadImage);
    }

    private static COMException CreateUnsupportedEditException()
    {
        return new COMException("Only a single, full-media HEIF-family edit at normal speed is supported.", HResultConst.Unsupported);
    }
}

