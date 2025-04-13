# sanshobara

Basics:
    Magic String (16 bytes)     | String | ~ |
    Format Version (4 bytes)    | String | ~ |
    Camera Tag (8 bytes)        | String | ~ |
    Camera Model (32 bytes)     | String | ~ |
 
Offsets:
    Firmware Version (4 bytes)  | String | ~ |
    <Unknown> (20 bytes)        | ~ |  ~ |
    JPEG Image Offset (4 bytes) | Int32 | BigEndian |
    JPEG Image Length (4 bytes) | Int32 | BigEndian |
    CFA Header Offset (4 bytes) | Int32 | BigEndian |
    CFA Header Length (4 bytes) | Int32 | BigEndian |
    CFA Record Offset (4 bytes) | Int32 | BigEndian |
    CFA Record Length (4 bytes) | Int32 | BigEndian |

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

/// https://developer.adobe.com/content/dam/udp/en/open/standards/tiff/TIFF6.pdf
/// https://developer.adobe.com/content/dam/udp/en/open/standards/tiff/TIFFPM6.pdf