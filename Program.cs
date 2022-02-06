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
        IFD Record Offset (4 bytes)
    
    @IFD Record Offset:
        Field Count (2 bytes)
        Fields:
            Field Tag (2 bytes)
            Field Type (2 bytes)
            Value Count (4 bytes)
            Value Offset (4 bytes)
        IFD Record Offset (4 bytes)
**/


/// File:
var file = File.Open(args[0], FileMode.Open, FileAccess.Read);



/// RAF Offsets:
var RafHeader = new byte[24];

file.Position = 84;
file.Read(RafHeader);

var JpegImageOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[0..]);
var JpegImageLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[4..]);
var CfaHeaderOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[8..]);
var CfaHeaderLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[12..]);
var CfaRecordOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[16..]);
var CfaRecordLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[20..]);

Console.WriteLine("JPEG Image:\tOffset = {0:X};\tLength = {1:X}", JpegImageOffset, JpegImageLength);
Console.WriteLine("CFA Header:\tOffset = {0:X};\tLength = {1:X}", CfaHeaderOffset, CfaHeaderLength);
Console.WriteLine("CFA Record:\tOffset = {0:X};\tLength = {1:X}", CfaRecordOffset, CfaRecordLength);



/// TIF Offsets:
var TifHeader = new byte[8];

file.Position = CfaRecordOffset;
file.Read(TifHeader);

var IfdRecordOffset = BinaryPrimitives.ReadInt32LittleEndian(TifHeader[4..]);

Console.WriteLine("IFD Record:\tOffset = {0:X}", IfdRecordOffset);



/// IFD Root Header:
var IfdHeader = new byte[2];

file.Position = CfaRecordOffset + IfdRecordOffset;
file.Read(IfdHeader);

var IfdFieldCount = BinaryPrimitives.ReadInt16LittleEndian(IfdHeader[0..]);

Console.WriteLine("IFD Record:\tFields = {0:X}", IfdFieldCount);


/// IFD Root Fields:
var IfdField = new byte[12];

file.Position = CfaRecordOffset + IfdRecordOffset + 2;
file.Read(IfdField);

var tag = BinaryPrimitives.ReadInt16LittleEndian(IfdField[0..]);
var type = BinaryPrimitives.ReadInt16LittleEndian(IfdField[2..]);
var count = BinaryPrimitives.ReadInt32LittleEndian(IfdField[4..]);
var value = BinaryPrimitives.ReadInt32LittleEndian(IfdField[8..]);

Console.WriteLine("\nIFD Field:\tTag = {0:X};\tType = {1:X}", tag, type);
Console.WriteLine("IFD Field:\tCount = {0};\tValue = {1}", count, value);


/// https://www.adobe.io/content/dam/udp/en/open/standards/tiff/TIFFPM6.pdf - Tech Note 1: TIFF Trees
/// IFD Leaf Fields: 
var IfdSubdir = new byte[12];

for (int i = 0; i < 16; ++i)
{
    file.Position = CfaRecordOffset + 0x1A + 2 + (12 * i);
    file.Read(IfdSubdir);

    tag = BinaryPrimitives.ReadInt16LittleEndian(IfdSubdir[0..]);
    type = BinaryPrimitives.ReadInt16LittleEndian(IfdSubdir[2..]);
    count = BinaryPrimitives.ReadInt32LittleEndian(IfdSubdir[4..]);
    value = BinaryPrimitives.ReadInt32LittleEndian(IfdSubdir[8..]);

    Console.WriteLine("\nIFD Field #{2}:\tTag = {0:X};\tType = {1:X}", tag, type, i);
    Console.WriteLine("IFD Field #{2}:\tCount = {0};\tValue = {1}", count, value, i);
}



// CFA Data:
var buffer = new byte[4992 * 3296 * 2];

file.Position = CfaRecordOffset + 0x800;
file.Read(buffer);

using (var image = new Image<L8>(4992, 3296))
{
    var offset = 0;
    for (int y = 0; y < image.Height; ++y)
    {
        for (int x = 0; x < image.Width; ++x)
        {
            offset = (y * image.Width + x) * 2;
            image[x, y] = new L8((byte) (BinaryPrimitives.ReadInt16LittleEndian(buffer[offset..(offset + 2)]) / 64));
        }
    }
    image.SaveAsPng("img/DSCF.png");
}