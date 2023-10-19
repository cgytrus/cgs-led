using System.Text.Json;
using System.Text.Json.Serialization;

namespace CgsLedServiceTypes;

public static class ConfigFile {
    private static readonly JsonSerializerOptions jsonOpts = new(JsonSerializerOptions.Default) {
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static TConfig LoadOrSave<TConfig>(string dir, string file, TConfig current) =>
        LoadOrSave(Path.Combine(dir, file), current);
    public static TConfig LoadOrSave<TConfig>(string path, TConfig current) {
        if(File.Exists(path))
            return JsonSerializer.Deserialize<TConfig>(File.ReadAllText(path), jsonOpts) ?? current;
        Save(path, current);
        return current;
    }
    public static void Save<TConfig>(string dir, string file, TConfig current) =>
        Save(Path.Combine(dir, file), current);
    public static void Save<TConfig>(string path, TConfig current) {
        string? dir = Path.GetDirectoryName(path);
        if(dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(current, jsonOpts));
    }

    public static object? LoadOrSave(string dir, string file, object? current) =>
        LoadOrSave(Path.Combine(dir, file), current);
    public static object? LoadOrSave(string path, object? current) {
        if(File.Exists(path))
            return JsonSerializer.Deserialize(File.ReadAllText(path), current?.GetType() ?? typeof(object), jsonOpts) ??
                current;
        Save(path, current);
        return current;
    }
    public static void Save(string dir, string file, object? current) =>
        Save(Path.Combine(dir, file), current);
    public static void Save(string path, object? current) {
        string? dir = Path.GetDirectoryName(path);
        if(dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(current, jsonOpts));
    }
}
