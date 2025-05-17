using System.Text.Json.Serialization;
using Oscinator.Core.VRChat;

namespace Oscinator.Core;

[JsonSerializable(typeof(VrcConfigsJsonSupport.ConfigJson))]
[JsonSerializable(typeof(VrcConfigsJsonSupport.AvatarParameterJson))]
[JsonSerializable(typeof(VrcConfigsJsonSupport.AvatarParameterValueJson))]
[JsonSerializable(typeof(VrcConfigsJsonSupport.AvatarParameterBinding))]
[JsonSerializable(typeof(VrcConfigsJsonSupport.AvatarSaveStateJson))]
public partial class GeneratedJsonSerializers : JsonSerializerContext
{
    
}