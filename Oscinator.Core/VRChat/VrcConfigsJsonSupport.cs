using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Oscinator.Core.VRChat;

public static class VrcConfigsJsonSupport
{
    public static ConfigJson? TryLoadConfig(string avatarId)
    {
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appDataRoaming, "..", "LocalLow", "VRChat", "VRChat", "OSC");
        var possibleUsers = Directory.EnumerateDirectories(baseDir, "*", SearchOption.TopDirectoryOnly);
        foreach (var possibleUser in possibleUsers)
        {
            var targetFile = Path.Combine(possibleUser, "Avatars", avatarId + ".json");
            if (!File.Exists(targetFile)) continue;

            try
            {
                using var file = File.OpenRead(targetFile);
                return JsonSerializer.Deserialize(file, GeneratedJsonSerializers.Default.ConfigJson);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "VRChat OSC Config parsing failed for avatarId {AvatarId} at path {Path}", avatarId, targetFile);
            }
        }

        return null;
    }
    
    public static async Task<AvatarSaveStateJson?> TryLoadSavedParams(string avatarId)
    {
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appDataRoaming, "..", "LocalLow", "VRChat", "VRChat", "LocalAvatarData");
        var possibleUsers = Directory.EnumerateDirectories(baseDir, "*", SearchOption.TopDirectoryOnly);
        foreach (var possibleUser in possibleUsers)
        {
            var targetFile = Path.Combine(possibleUser, avatarId);
            if (!File.Exists(targetFile)) continue;

            try
            {
                await using var file = File.OpenRead(targetFile);
                return await JsonSerializer.DeserializeAsync(file, GeneratedJsonSerializers.Default.AvatarSaveStateJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "VRChat saved parameter parsing failed for avatarId {AvatarId} at path {Path}", avatarId, targetFile);
            }
        }

        return null;
    }

    public class ConfigJson
    {
        [JsonPropertyName("id")] 
        public string Id { get; set; } = "";

        [JsonPropertyName("name")] 
        public string Name { get; set; } = "";

        [JsonPropertyName("parameters")]
        public List<AvatarParameterJson> Parameters { get; set; } = new();
    }

    public class AvatarParameterJson
    {
        [JsonPropertyName("name")] 
        public string Name { get; set; } = "";
        
        [JsonPropertyName("input")]
        public AvatarParameterBinding? Input { get; set; }
        
        [JsonPropertyName("output")]
        public AvatarParameterBinding? Output { get; set; }
    }

    public class AvatarParameterBinding
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = "";

        [JsonPropertyName("type")] 
        public string Type { get; set; } = "";
    }

    public class AvatarParameterValueJson
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("value")]
        public float Value { get; set; }
    }

    public class AvatarSaveStateJson
    {
        [JsonPropertyName("animationParameters")]
        public List<AvatarParameterValueJson> Values { get; set; } = new();
        
        [JsonPropertyName("eyeHeight")]
        public float EyeHeight { get; set; }
    }

    private static readonly ILogger Logger = LogUtils.LoggerFor(typeof(VrcConfigsJsonSupport));
}