using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;

namespace RightRule_Developer;

public sealed class SessionDocument
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, Dictionary<string, JsonElement>> Datasets { get; set; } = new();
    public List<RuleDocument> Rules { get; set; } = new();
    public List<RuleDocument> Defaults { get; set; } = new();
}
public sealed class RuleDocument
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Context { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Decision { get; set; } = "";
    public string DecisionData { get; set; } = "";
    public string OptionalData { get; set; } = "";
}
public static class SessionFiles
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RightRule Developer", "Sessions");
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static IEnumerable<string> List() => Directory.Exists(Root) ? Directory.GetDirectories(Root).Where(d => File.Exists(Path.Combine(d, "session.json"))).Select(d => Path.GetFileName(d)!) : [];
    public static string FileFor(string name) => Path.Combine(Root, name, "session.json");
    public static SessionDocument Read(string path) => JsonSerializer.Deserialize<SessionDocument>(File.ReadAllText(path), JsonOptions) ?? new();
    public static void Write(SessionDocument doc) { var path = FileFor(doc.Name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(doc, JsonOptions)); }
}
