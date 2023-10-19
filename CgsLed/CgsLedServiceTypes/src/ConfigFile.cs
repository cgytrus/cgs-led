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

    public static TConfig LoadOrSave<TConfig>(string dir, string file, TConfig current) {
        string configPath = Path.Combine(dir, file);
        if(File.Exists(configPath))
            return JsonSerializer.Deserialize<TConfig>(File.ReadAllText(configPath), jsonOpts) ?? current;
        Directory.CreateDirectory(dir);
        File.WriteAllText(configPath, JsonSerializer.Serialize(current, jsonOpts));
        return current;
    }

    public static object? LoadOrSave(string dir, string file, object? current) {
        string configPath = Path.Combine(dir, file);
        if(File.Exists(configPath))
            return JsonSerializer.Deserialize(File.ReadAllText(configPath), current?.GetType() ?? typeof(object),
                jsonOpts) ?? current;
        Directory.CreateDirectory(dir);
        File.WriteAllText(configPath, JsonSerializer.Serialize(current, jsonOpts));
        return current;
    }
}
