using System.Buffers.Binary;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleApp;

class Program
{
    private static FileStream? RafFile;
    private static byte[]? RafHeader;

    private static int JpegImageOffset;
    private static int JpegImageLength;
    private static int CfaHeaderOffset;
    private static int CfaHeaderLength;
    private static int CfaRecordOffset;
    private static int CfaRecordLength;

    static async Task<int> Main(string[] args)
    {
        /// Options: ///

        var fileOption = new Option<FileInfo?>(
        name: "--file",
        description: "The file to read and display on the console.")
        { IsRequired = true };

        /// Commands: ///

        /// Root Command:
        var rootCommand = new RootCommand("Utility application for working with Fujifilm RAF image files.");
        rootCommand.AddGlobalOption(fileOption);

        /// Info Command:
        var infoCommand = new Command("info", "Extract and display metadata.") { fileOption };
        rootCommand.AddCommand(infoCommand);
        infoCommand.SetHandler(async (file) => { await WriteInfo(); });

        /// Inject Command:
        var injectCommand = new Command("inject", "Inject image data from a bitmap image into the RAF file.") { fileOption };
        rootCommand.AddCommand(injectCommand);
        injectCommand.SetHandler(async (file) => { await InjectImage(); });

        /// Extract Command:
        var extractCommand = new Command("extract", "Extract bracketed image data from an HDR RAF file.") { fileOption };
        rootCommand.AddCommand(extractCommand);
        extractCommand.SetHandler(async (file) => { await ExtractImage(); });

        /// Develop Command:
        var developCommand = new Command("develop", "Process the CFA data in the RAF file and generate a bitmap image.") { fileOption };
        rootCommand.AddCommand(developCommand);
        developCommand.SetHandler(async (file) => { await DevelopImage(); });

        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        commandLineBuilder.AddMiddleware(async (context, next) => { await ReadFile(context.ParseResult.GetValueForOption(fileOption)!); await next(context); });
        return commandLineBuilder.UseDefaults().Build().InvokeAsync(args).Result;
    }

    internal static async Task ReadFile(FileInfo fileInfo)
    {
        /// RAF File:

        RafFile = fileInfo.Open(FileMode.Open, FileAccess.Read);

        /// RAF Offsets:

        RafHeader = new byte[24];

        RafFile.Position = 84;
        RafFile.ReadExactly(RafHeader);

        JpegImageOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[0..]);
        JpegImageLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[4..]);
        CfaHeaderOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[8..]);
        CfaHeaderLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[12..]);
        CfaRecordOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[16..]);
        CfaRecordLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[20..]);
    }

    internal static async Task WriteInfo()
    {
        /// RAF Offsets:

        Console.WriteLine("JPEG Image:\tOffset = 0x{0:X};\tLength = 0x{1:X}", JpegImageOffset, JpegImageLength);
        Console.WriteLine("CFA Header:\tOffset = 0x{0:X};\tLength = 0x{1:X}", CfaHeaderOffset, CfaHeaderLength);
        Console.WriteLine("CFA Record:\tOffset = 0x{0:X};\tLength = 0x{1:X}", CfaRecordOffset, CfaRecordLength);

        Console.WriteLine("~~~");

        // -------------------------------------------------------------------------- //

        /// TIF Offsets:

        var TifHeader = new byte[8];

        RafFile.Position = CfaRecordOffset;
        RafFile.ReadExactly(TifHeader);

        var IfdHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(TifHeader[4..]);

        Console.WriteLine("IFD Header:\tOffset = 0x{0:X}", IfdHeaderOffset);

        Console.WriteLine("~~~");

        // -------------------------------------------------------------------------- //

        /// IFD Header:

        var IfdHeader = new byte[2];

        RafFile.Position = CfaRecordOffset + IfdHeaderOffset;
        RafFile.ReadExactly(IfdHeader);

        var IfdRecordCount = BinaryPrimitives.ReadInt16LittleEndian(IfdHeader[0..]);

        Console.WriteLine("IFD Header:\tCount = {0}", IfdRecordCount);

        Console.WriteLine("~~~");

        // -------------------------------------------------------------------------- //

        /// IFD Record:

        var IfdRecord = new byte[12];

        RafFile.Position = CfaRecordOffset + IfdHeaderOffset + 2;
        RafFile.ReadExactly(IfdRecord);

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
        RafFile.ReadExactly(SubIfdHeader);

        var SubIfdRecordCount = BinaryPrimitives.ReadInt16LittleEndian(SubIfdHeader[0..]);

        Console.WriteLine("SubIFD Header:\tCount = {0}", SubIfdRecordCount);

        Console.WriteLine("~~~");

        // -------------------------------------------------------------------------- //

        /// SubIFD Records:

        for (int i = 0; i < SubIfdRecordCount; ++i)
        {
            var SubIfdRecord = new byte[12];

            RafFile.Position = CfaRecordOffset + SubIfdHeaderOffset + 2 + (i * 12);
            RafFile.ReadExactly(SubIfdRecord);

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
    }

    internal static async Task InjectImage()
    {
        /// HALD Injection:

        int[][] ColorFilterArray = [
            [1, 1, 0, 1, 1, 2],
            [1, 1, 2, 1, 1, 0],
            [2, 0, 1, 0, 2, 1],
            [1, 1, 2, 1, 1, 0],
            [1, 1, 0, 1, 1, 2],
            [0, 2, 1, 2, 0, 1]
        ];

        var buffer16 = new byte[6384 * 4182 * 2].AsSpan();

        RafFile.Position = CfaRecordOffset + 0x800;
        RafFile.ReadExactly(buffer16);

        var buffer24 = new byte[6384 * 4182 * 3].AsSpan();

        using (var image = Image.Load<Rgb24>("TODO"))
        {
            image.CopyPixelDataTo(buffer24);
        }

        for (int y = 0; y < 4182; ++y)
        {
            for (int x = 0; x < 6384; ++x)
            {
                var offset16 = (y * 6384 + x) * 2;
                var offset24 = (y * 6384 + x) * 3 + ColorFilterArray[y % 6][x % 6];
                var value = (ushort)(buffer24[offset24] * (((2 << 13) - (2 << 7)) / ((2 << 7) - 1)) + ((2 << 7) - 1));

                BinaryPrimitives.WriteUInt16LittleEndian(buffer16.Slice(offset16, 2), value);
            }
        }

        RafFile.Position = CfaRecordOffset + 0x800;
        RafFile.Write(buffer16);
    }

    internal static async Task ExtractImage()
    {
        /// HDR Extraction:

        var HdrHeader = new byte[CfaRecordOffset].AsSpan();

        RafFile.Position = 0;
        RafFile.ReadExactly(HdrHeader);

        RafFile.Position = 0;
        foreach (var exposureValue in new[] { "±0", "-1", "+1" })
        {
            RafFile.Position += 84;
            RafFile.ReadExactly(RafHeader);

            CfaRecordOffset = BinaryPrimitives.ReadInt32BigEndian(RafHeader[16..]);
            CfaRecordLength = BinaryPrimitives.ReadInt32BigEndian(RafHeader[20..]);

            var HdrBuffer = new byte[CfaRecordLength].AsSpan();

            RafFile.Position -= 84 + 24;
            RafFile.Position += CfaRecordOffset;
            RafFile.ReadExactly(HdrBuffer);

            using (var HdrFile = File.Open(Path.Combine(Path.GetDirectoryName(RafFile.Name) ?? "", Path.GetFileNameWithoutExtension(RafFile.Name) + $" ({exposureValue}EV).RAF"), FileMode.Create, FileAccess.Write))
            {
                HdrFile.Write(HdrHeader);
                HdrFile.Write(HdrBuffer);
            }
        }
    }

    internal static async Task DevelopImage()
    {
        /// CFA Records:

        var buffer16 = new byte[6384 * 4182 * 2].AsSpan();

        RafFile.Position = CfaRecordOffset + 0x800;
        RafFile.ReadExactly(buffer16);

        using (var image = new Image<L8>(6384, 4182))
        {
            var offset = 0;
            var value = 0;
            for (int y = 0; y < image.Height; ++y)
            {
                for (int x = 0; x < image.Width; ++x)
                {
                    offset = (y * image.Width + x) * 2;
                    value = BinaryPrimitives.ReadInt16LittleEndian(buffer16.Slice(offset, 2));
                    image[x, y] = new L8((byte)(value / (2 << 5)));
                }
            }
            image.SaveAsPng("img/DSCF.png");
        }
    }

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
}