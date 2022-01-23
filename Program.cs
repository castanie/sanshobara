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
    EXIF (...)

CFA Header Offset:
    Record count (4 bytes)
    Records:
        Record id (2 bytes)
        Record size (2 bytes)
        Data (...)

CFA Record Offset:
    Data (...)
**/

var CFAHeaderOffset = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(92));
var CFAHeaderLength = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(96));
var CFARecordOffset = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(100));
var CFARecordLength = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(104));

Console.WriteLine("{0:x}, {1:x}", CFAHeaderOffset, CFAHeaderLength);
Console.WriteLine("{0:x}, {1:x}", CFARecordOffset, CFARecordLength);

/*
using (Image<L8> image = new Image<L8>(4896, 3264))
{
    for (int y = 0; y < image.Height; ++y)
    {
        for (int x = 0; x < image.Width; ++x)
        {
            image[x, y] = new L8((byte) (BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(file, (int) start, 2)) / 255));
            start += 2;
        }
    }

    image.SaveAsPng("img/DSCF.png");
}
*/