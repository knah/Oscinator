using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NanoOsc;
using Vrc.OscQuery;

namespace Oscinator.Core;

public partial class AvatarState
{
    public readonly OscQueryServiceProfile? ServiceProfile;
    public readonly HostInfo HostInfo;

    public readonly IApplicationSupport ApplicationSupport;
    private readonly ConcurrentDictionary<string, ParameterVariant> myParameters = new();
    private readonly ConcurrentDictionary<string, bool> myParametersReadOnly = new();
    private readonly ConcurrentDictionary<string, string> myParameterDisplayNames = new();

    public event Action<string>? ParameterChange;
    public event Action? ParametersBulkChange;

    public ParameterVariant? this[string name] => myParameters.TryGetValue(name, out var result) ? result : null;
    public IEnumerable<KeyValuePair<string, ParameterVariant>> Parameters => myParameters;

    public bool IsParameterReadOnly(string parameterName) =>
        myParametersReadOnly.TryGetValue(parameterName, out var result) && result ||
        ApplicationSupport.ReadOnlyParameters.Contains(parameterName);

    public void Clear()
    {
        myParameters.Clear();
        myParametersReadOnly.Clear();
        ParametersBulkChange?.Invoke();
    }

    public void FetchCurrentAvatarId()
    {
        FetchCurrentAvatarIdAndParametersAsync().NoAwait(Logger, "Fetching current parameters");
    }

    private async Task FetchCurrentAvatarIdAndParametersAsync()
    {
        var oscqService = ServiceProfile;
        if (oscqService == null) return;
        
        var avatarNode = await Utils.GetOscTree(oscqService.Address, oscqService.Port, "/avatar");
        if (avatarNode == null || !avatarNode.Contents.TryGetValue("change", out var changeNode)) return;

        if (changeNode.Value?.FirstOrDefault() is not JsonElement { ValueKind: JsonValueKind.String } value) return;
        var avatarId = value.GetString()!;
        
        if (!avatarNode.Contents.TryGetValue("parameters", out var parametersNode)) return;
        
        var seenParameters = new HashSet<string>();
        ApplyParameterData(parametersNode, seenParameters, await ApplicationSupport.ReadStoredParameters(avatarId));
        var parametersToRemove = new HashSet<string>(myParameters.Keys);
        parametersToRemove.ExceptWith(ApplicationSupport.ReadOnlyParameters);
        parametersToRemove.ExceptWith(seenParameters);
        
        foreach (var s in parametersToRemove)
        {
            myParameters.TryRemove(s, out _);
            myParameterDisplayNames.TryRemove(s, out _);
            myParametersReadOnly.TryRemove(s, out _);
        }
        
        ParametersBulkChange?.Invoke();
    }

    private static readonly Regex UnicodeUnitEscapeRegex = GetUnicodeUnitEscapeRegex();

    public string GetDisplayName(string parameterName)
    {
        return myParameterDisplayNames.GetOrAdd(parameterName, ParseUnicodeEscapes);
    }

    private static string ParseUnicodeEscapes(string input)
    {
        return UnicodeUnitEscapeRegex.Replace(input, GetUnicodeStringFromPoint);
    }

    private static string GetUnicodeStringFromPoint(Match match)
    {
        var matchValue = match.Value;
        if (matchValue.Length < 6) throw new FormatException("Match too short, what?");
        if (matchValue[0] != '\\' || matchValue[1] != 'u') throw new FormatException("Match has unexpected prefix");
        var codePoint = int.Parse(matchValue[2..], NumberStyles.HexNumber);

        return Encoding.UTF32.GetString(MemoryMarshal.Cast<int, byte>(new Span<int>(ref codePoint)));
    }

    private void ApplyParameterData(OscQueryNode parametersNode, HashSet<string> seenParameters, IDictionary<string, ParameterVariant>? savedParameters)
    {
        foreach (var (_, value) in parametersNode.Contents) 
            ApplyParameterData(value, seenParameters, savedParameters);

        if (parametersNode.FullPath.Length <= AvatarParameterPrefixS.Length) return;
        
        var address = parametersNode.FullPath[AvatarParameterPrefixS.Length..];
        var type = parametersNode.OscType;
        var isRealParameter = parametersNode.Access != 0;

        if (isRealParameter && ApplicationSupport.ReturnsCorrectParameterPermissions) 
            myParametersReadOnly[address] = (parametersNode.Access & Attributes.AccessValues.Write) == 0;

        seenParameters.Add(address);

        if (myParameters.TryGetValue(address, out var existing) && existing.Type != ParameterType.Unknown)
            return;

        ParameterVariant newVariant = default;
        if (savedParameters?.TryGetValue(address, out var savedParameter) == true)
        {
            var savedFloat = savedParameter.ToFloat();
            newVariant = type switch
            {
                "i" => new ParameterVariant((int)savedFloat),
                "f" => new ParameterVariant(savedFloat),
                "T" => new ParameterVariant(savedFloat > 0),
                "F" => new ParameterVariant(savedFloat > 0),
                _ => default
            };
        }
        
        if (newVariant.Type == ParameterType.Unknown) 
            newVariant = ParseOscQueryParameter(parametersNode) ?? newVariant;

        if (newVariant.Type == ParameterType.Unknown && parametersNode.Value is not { Length: > 0 })
            return;

        var setValue = myParameters.AddOrUpdate(address, static (_, v) => v,
            static (_, old, value) => old.Type == ParameterType.Unknown ? value : old, newVariant);
        
        if (setValue == newVariant)
            ParameterChange?.Invoke(address);
    }

    private static ParameterVariant? ParseOscQueryParameter(OscQueryNode parametersNode)
    {
        var type = parametersNode.OscType;
        var values = parametersNode.Value;
        if (values == null || values.Length == 0) return null;
        var firstValue = values[0];

        if (string.IsNullOrEmpty(type) || type.Length != 1) return null;
        return firstValue switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } numberValue when type == "i" => new ParameterVariant(
                numberValue.GetInt32()),
            JsonElement { ValueKind: JsonValueKind.Number } numberValue when type == "f" => new ParameterVariant(
                numberValue.GetSingle()),
            JsonElement { ValueKind: JsonValueKind.True } => new ParameterVariant(true),
            JsonElement { ValueKind: JsonValueKind.False } => new ParameterVariant(false),
            _ => default
        };
    }

    private void AddParameter(string name, ParameterVariant value)
    {
        myParameters[name] = value;
        ParameterChange?.Invoke(name);
    }

    public void OnOscMessage(OscMessageParser reader)
    {
        if (reader.Address.StartsWith(AvatarParameterPrefix))
        {
            var parameterNameSpan = reader.Address[AvatarParameterPrefix.Length..];
            var parameterType = GetType(reader.PeekNextRawType());
            var parameterName = Encoding.UTF8.GetString(parameterNameSpan);
            switch (parameterType)
            {
                case ParameterType.Unknown:
                    Logger.LogWarning("Unsupported avatar parameter type: {Type} for {Parameter}", reader.PeekNextRawType(), parameterName);
                    AddParameter(parameterName, new ParameterVariant(parameterType));
                    break;
                case ParameterType.Float:
                    AddParameter(parameterName, new ParameterVariant(reader.ReadFloat()));
                    break;
                case ParameterType.Bool:
                    AddParameter(parameterName, new ParameterVariant(reader.ReadBool()));
                    break;
                case ParameterType.Int:
                    AddParameter(parameterName, new ParameterVariant(reader.ReadInt()));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        } else if (reader.Address.Length == AvatarChangePrefix.Length && reader.Address.StartsWith(AvatarChangePrefix))
        {
            if (reader.PeekNextRawType() == OscType.String)
            {
                var avatarId = reader.ReadString();
                FetchCurrentAvatarIdAndParametersAsync().NoAwait(Logger, $"Fetching parameters on avatar change to {Encoding.UTF8.GetString(avatarId)}");
            }
            else
            {
                Logger.LogWarning("Received avatar config event with bad argument type: {Type}", reader.PeekNextRawType());
            }
        }
    }

    private static ParameterType GetType(OscType oscType)
    {
        return oscType switch
        {
            OscType.False => ParameterType.Bool,
            OscType.True => ParameterType.Bool,
            OscType.Int => ParameterType.Int,
            OscType.Float => ParameterType.Float,
            _ => ParameterType.Unknown
        };
    }
    
    private static readonly ILogger Logger = LogUtils.LoggerFor<AvatarState>();

    public AvatarState(IApplicationSupport applicationSupport, HostInfo hostInfo, OscQueryServiceProfile? serviceProfile)
    {
        if (serviceProfile != null && serviceProfile.Type != OscQueryServiceProfile.ServiceType.OscQuery)
            throw new ArgumentException("Service must be of OSCQuery type", nameof(serviceProfile));
        
        ApplicationSupport = applicationSupport;
        ServiceProfile = serviceProfile;
        HostInfo = hostInfo;
    }

    public static ReadOnlySpan<byte> AvatarParameterPrefix => "/avatar/parameters/"u8;
    public const string AvatarParameterPrefixS = "/avatar/parameters/";
    public static ReadOnlySpan<byte> AvatarChangePrefix => "/avatar/change"u8;
    public const string AvatarChangePrefixS = "/avatar/change";

    [GeneratedRegex("\\\\u[0-9a-fA-F]{4,5}")]
    private static partial Regex GetUnicodeUnitEscapeRegex();
}