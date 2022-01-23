using System.Buffers.Binary;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var file = File.ReadAllBytes("img/DSCF.RAF");

/**
Basic:
    Magic string (16 bytes)
    Unknown (4 bytes)
    Unknown (8 bytes)
    Camera name (32 bytes)

Offsets:
    Version (4 bytes)
    Unknown (20 bytes)
    JPEG image offset (4 bytes)
    JPEG image length (4 bytes)
    CFA header offset (4 bytes)
    CFA header length (4 bytes)
    CFA record offset (4 bytes)
    CFA record length (4 bytes)

JPEG Image Offset:
    EXIF (...) - Little Endian!

CFA Header Offset:
    Record count (4 bytes)
    Records:
        ID (2 bytes)
        Size (2 bytes)
        Data (...)

CFA Record Offset:
    Data (...) - Little Endian!
**/

var CFAHeaderOffset = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(92));
var CFAHeaderLength = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(96));
var CFARecordOffset = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(100));
var CFARecordLength = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(104));

var CFARecordCount = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan((int) CFAHeaderOffset));
var CFARecords = new Record[CFARecordCount];

/*
var _position = 0;
for (int i = 0; i < CFARecordCount; ++i)
{
    var id = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan((int) CFAHeaderOffset + _position));
    var size = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan((int) CFAHeaderOffset + _position));
    CFARecords[i] = new Record();
}
*/

Console.WriteLine("Header: offset = {0:x}, length = {1:x}", CFAHeaderOffset, CFAHeaderLength);
Console.WriteLine("Record: offset = {0:x}, length = {1:x}", CFARecordOffset, CFARecordLength);
Console.WriteLine("Record: count = {0:d}", CFARecordCount);


var start = CFARecordOffset;

using (Image<L8> image = new Image<L8>(4992, 3296))
{
    for (int y = 0; y < image.Height; ++y)
    {
        for (int x = 0; x < image.Width; ++x)
        {
            image[x, y] = new L8((byte) (BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(file, (int) start, 2)) / 64));
            start += 2;
        }
    }

    image.SaveAsPng("img/DSCF.png");
}


class Record {
    readonly short ID;
    readonly short Size;
    readonly byte[] Data;

    public Record() {
        this.ID = 0;
        this.Size = 0;
        this.Data = new byte[] {};
    }
}