using System.IO;

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
    public static Task<ParsedSave> ParseAsync(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save file path is required.", nameof(savePath));
        if (!File.Exists(savePath)) throw new FileNotFoundException("The selected save file does not exist.", savePath);

        throw new NotImplementedException("Native Palworld save parsing is not implemented yet. The external Python parser has been removed; direct C# parsing will replace it in the next step.");
    }
}
