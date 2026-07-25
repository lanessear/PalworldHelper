using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PalworldHelper;

public sealed class ParsedSave
{
    public string Parser { get; set; } = "";
    public int SaveType { get; set; }
    public int PlayerCount { get; set; }
    public int PalCount { get; set; }
    public List<ParsedPlayer> Players { get; set; } = [];
    public List<ParsedPal> Pals { get; set; } = [];
}

public sealed class ParsedPlayer
{
    public string Name { get; set; } = "";
    public string PlayerUid { get; set; } = "";
    public int Level { get; set; }
}

public sealed class ParsedPal
{
    public string Owner { get; set; } = "";
    public string OwnerPlayerUid { get; set; } = "";
    public string Species { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public string Gender { get; set; } = "";
    public List<string> PassiveSkills { get; set; } = [];
    public string InstanceId { get; set; } = "";
    public string PassiveSkillsText => string.Join(", ", PassiveSkills);
}

public static class SaveParserService
{
    private const string GvasMagic = "GVAS";
    private const string OodleMagic = "PlM";
    private const string ZlibMagic = "PlZ";
    private const int HeaderLength = 12;
    private const int ConsoleHeaderPrefixLength = 12;

    public static async Task<ParsedSave> ParseAsync(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save file path is required.", nameof(savePath));
        if (!File.Exists(savePath)) throw new FileNotFoundException("The selected save file does not exist.", savePath);

        var saveBytes = await File.ReadAllBytesAsync(savePath).ConfigureAwait(false);
        var container = PalworldSaveContainer.Read(saveBytes);
        var gvasBytes = container.Decompress();
        if (!StartsWithMagic(gvasBytes, GvasMagic))
        {
            throw new InvalidDataException("The decompressed save data does not start with a GVAS header.");
        }

        return new ParsedSave
        {
            Parser = "native-csharp",
            SaveType = container.SaveType
        };
    }

    private static bool StartsWithMagic(ReadOnlySpan<byte> bytes, string magic)
    {
        return bytes.Length >= magic.Length && Encoding.ASCII.GetString(bytes[..magic.Length]) == magic;
    }

    private sealed record PalworldSaveContainer(int UncompressedLength, int CompressedLength, string Magic, int SaveType, byte[] Body)
    {
        public static PalworldSaveContainer Read(byte[] saveBytes)
        {
            if (StartsWithMagic(saveBytes, GvasMagic))
            {
                return new PalworldSaveContainer(saveBytes.Length, saveBytes.Length, GvasMagic, 0, saveBytes);
            }

            if (TryRead(saveBytes, 0, out var container)) return container;
            if (TryRead(saveBytes, ConsoleHeaderPrefixLength, out container)) return container;

            throw new InvalidDataException("The selected file is not a supported Palworld .sav container.");
        }

        public byte[] Decompress()
        {
            return Magic switch
            {
                GvasMagic => Body,
                ZlibMagic when SaveType == 0x31 => Inflate(Body, UncompressedLength),
                ZlibMagic when SaveType == 0x32 => Inflate(Inflate(Body, CompressedLength), UncompressedLength),
                OodleMagic => throw new NotSupportedException("This save uses Oodle Mermaid compression (PlM). Native Oodle decompression is not available yet."),
                _ => throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Unsupported Palworld save container '{Magic}' with save type 0x{SaveType:X2}."))
            };
        }

        private static bool TryRead(byte[] saveBytes, int offset, out PalworldSaveContainer container)
        {
            container = null!;
            if (saveBytes.Length < offset + HeaderLength) return false;

            var magic = Encoding.ASCII.GetString(saveBytes, offset + 8, 3);
            if (magic is not (OodleMagic or ZlibMagic)) return false;

            var uncompressedLength = BinaryPrimitives.ReadInt32LittleEndian(saveBytes.AsSpan(offset, sizeof(int)));
            var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(saveBytes.AsSpan(offset + 4, sizeof(int)));
            var saveType = saveBytes[offset + 11];
            var bodyOffset = offset + HeaderLength;
            if (uncompressedLength <= 0 || compressedLength <= 0 || saveBytes.Length < bodyOffset + compressedLength)
            {
                throw new InvalidDataException("The Palworld save container header is invalid or truncated.");
            }

            var body = new byte[compressedLength];
            Buffer.BlockCopy(saveBytes, bodyOffset, body, 0, compressedLength);
            container = new PalworldSaveContainer(uncompressedLength, compressedLength, magic, saveType, body);
            return true;
        }

        private static byte[] Inflate(byte[] compressed, int expectedLength)
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = expectedLength > 0 ? new MemoryStream(expectedLength) : new MemoryStream();
            zlib.CopyTo(output);
            var result = output.ToArray();
            if (expectedLength > 0 && result.Length != expectedLength)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Decompressed save size is {result.Length:N0} bytes, expected {expectedLength:N0} bytes."));
            }

            return result;
        }
    }
}
