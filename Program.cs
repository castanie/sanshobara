using System.Buffers.Binary;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/**
Basics:
    Magic String (16 bytes)
    <Unknown> (4 bytes)
    Camera Tag (8 bytes)
    Camera Model (32 bytes)

Offsets:
    Version (4 bytes)
    <Unknown> (20 bytes)
    JPEG Image Offset (4 bytes)
    JPEG Image Length (4 bytes)
    CFA Header Offset (4 bytes)
    CFA Header Length (4 bytes)
    CFA Record Offset (4 bytes)
    CFA Record Length (4 bytes)

-------------------------------

@JPEG Image Offset:
    EXIF (...) - Little Endian!

@CFA Header Offset:
    Record Count (4 bytes)
    Records:
        ID (2 bytes)
        Size (2 bytes)
        Data ({Size} bytes)

@CFA Record Offset:
    TIFF (...) - Little Endian!
        Byte Order (2 bytes)
        Magic Number (2 bytes)
        IFD Header Offset (4 bytes)
    
    @IFD Header Offset:
        Field Count (2 bytes)
        Fields:
            Field Tag (2 bytes)
            Field Type (2 bytes)
            Value Count (4 bytes)
            Value Offset (4 bytes)
        IFD Header Offset (4 bytes)
**/

/// https://developer.adobe.com/content/dam/udp/en/open/standards/tiff/TIFF6.pdf
/// https://developer.adobe.com/content/dam/udp/en/open/standards/tiff/TIFFPM6.pdf

// -------------------------------------------------------------------------- //

/// RAF File:

var RafFile = File.Open(args[0], FileMode.Open, FileAccess.Read);

// -------------------------------------------------------------------------- //

/// RAF Offsets:

var RafHeader = new byte[24];

RafFile.Position = 84;
RafFile.Read(RafHeader);

var JpegImageOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[0..]);
var JpegImageLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[4..]);
var CfaHeaderOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[8..]);
var CfaHeaderLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[12..]);
var CfaRecordOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[16..]);
var CfaRecordLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[20..]);

Console.WriteLine("JPEG Image:\tOffset = 0x{0:X};\tLength = 0x{1:X}", JpegImageOffset, JpegImageLength);
Console.WriteLine("CFA Header:\tOffset = 0x{0:X};\tLength = 0x{1:X}", CfaHeaderOffset, CfaHeaderLength);
Console.WriteLine("CFA Record:\tOffset = 0x{0:X};\tLength = 0x{1:X}", CfaRecordOffset, CfaRecordLength);

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// TIF Offsets:

var TifHeader = new byte[8];

RafFile.Position = CfaRecordOffset;
RafFile.Read(TifHeader);

var IfdHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(TifHeader[4..]);

Console.WriteLine("IFD Header:\tOffset = 0x{0:X}", IfdHeaderOffset);

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// IFD Header:

var IfdHeader = new byte[2];

RafFile.Position = CfaRecordOffset + IfdHeaderOffset;
RafFile.Read(IfdHeader);

var IfdRecordCount = BinaryPrimitives.ReadInt16LittleEndian(IfdHeader[0..]);

Console.WriteLine("IFD Header:\tCount = {0}", IfdRecordCount);

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// IFD Record:

var IfdRecord = new byte[12];

RafFile.Position = CfaRecordOffset + IfdHeaderOffset + 2;
RafFile.Read(IfdRecord);

var ifdTag = BinaryPrimitives.ReadInt16LittleEndian(IfdRecord[0..]);
var ifdType = BinaryPrimitives.ReadInt16LittleEndian(IfdRecord[2..]);
var ifdCount = BinaryPrimitives.ReadInt32LittleEndian(IfdRecord[4..]);
var ifdValue = BinaryPrimitives.ReadInt32LittleEndian(IfdRecord[8..]);

var SubIfdHeaderOffset = ifdValue;

Console.WriteLine("SubIFD Header:\tOffset = 0x{0:X}", SubIfdHeaderOffset);

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// SubIFD Header:

var SubIfdHeader = new byte[2];

RafFile.Position = CfaRecordOffset + SubIfdHeaderOffset;
RafFile.Read(SubIfdHeader);

var SubIfdRecordCount = BinaryPrimitives.ReadInt16LittleEndian(SubIfdHeader[0..]);

Console.WriteLine("SubIFD Header:\tCount = {0}", SubIfdRecordCount);

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// SubIFD Records:

for (int i = 0; i < SubIfdRecordCount; ++i)
{
    var SubIfdRecord = new byte[12];

    RafFile.Position = CfaRecordOffset + SubIfdHeaderOffset + 2 + (i * 12);
    RafFile.Read(SubIfdRecord);

    var subIfdTag = BinaryPrimitives.ReadInt16LittleEndian(SubIfdRecord[0..]);
    var subIfdType = BinaryPrimitives.ReadInt16LittleEndian(SubIfdRecord[2..]);
    var subIfdCount = BinaryPrimitives.ReadInt32LittleEndian(SubIfdRecord[4..]);
    var subIfdValue = BinaryPrimitives.ReadInt32LittleEndian(SubIfdRecord[8..]);

    Console.WriteLine(
        "SubIFD Field:\tTag = {0:X};\tType = {1};\tCount = {2};\tValue = {3}",
        subIfdTag,
        Enum.GetName(typeof(IfdType), subIfdType),
        subIfdCount,
        subIfdValue
    );
}

Console.WriteLine("~~~");

// -------------------------------------------------------------------------- //

/// CFA Records:

var buffer = new byte[6384 * 4182 * 2];

RafFile.Position = CfaRecordOffset + 0x800;
RafFile.Read(buffer);

using (var image = new Image<L8>(6384, 4182))
{
    var offset = 0;
    for (int y = 0; y < image.Height; ++y)
    {
        for (int x = 0; x < image.Width; ++x)
        {
            offset = (y * image.Width + x) * 2;
            image[x, y] = new L8((byte)(BinaryPrimitives.ReadInt16LittleEndian(buffer[offset..(offset + 2)]) / 64));
        }
    }
    image.SaveAsPng("img/DSCF.png");
}

// -------------------------------------------------------------------------- //

enum IfdType : uint
{
    Byte = 1,
    Ascii = 2,
    Short = 3,
    Long = 4,
    Ratio = 5,
    SByte = 6,
    Undefined = 7,
    SShort = 8,
    SLong = 9,
    SRatio = 10,
    Float = 11,
    Double = 12,
    Ifd = 13
}