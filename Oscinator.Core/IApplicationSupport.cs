using System.Collections.Frozen;

namespace Oscinator.Core;

public interface IApplicationSupport
{
    public const int NeutralMatchWeight = 0;
    public const int WeakMatchWeight = 10;
    public const int GoodMatchWeight = 100;
    public const int PerfectMatchWeight = 1000;

    private static readonly Task<IDictionary<string, ParameterVariant>?> NoParameters = Task.FromResult<IDictionary<string, ParameterVariant>?>(null);
    
    /// <summary>
    /// Some poorly-behaved applications provide wrong parameter values via OSCQuery.
    /// This method can be used to provide correct values, i.e. by reading them from application's storage.  
    /// </summary>
    /// <param name="avatarId">Avatar ID to read config for, as reported by /avatar/change OSC endpoint</param>
    public Task<IDictionary<string, ParameterVariant>?> ReadStoredParameters(string avatarId) => NoParameters;

    /// <summary>
    /// Specifies if parameter permissions reported by OSCQuery are correct. If not, `ReadOnlyParameters` will be used.
    /// </summary>
    public bool ReturnsCorrectParameterPermissions => true;
    
    public IReadOnlySet<string> ReadOnlyParameters => FrozenSet<string>.Empty;
    
    /// <summary>
    /// The returned weight is used to match support classes to services. Highest weighted service would be chosen.
    /// Returning null indicates this support class is not applicable. 
    /// </summary>
    public int? MatchRemoteName(string name);
    
    /// <summary>
    /// Some poorly-behaved applications use random send ports that they don't advertise as their OSC port.
    /// For those applications, it's impossible to attribute incoming OSC messages to a specific instance.
    /// Returning true from here would merge all applications on a given IP into one, possibly of a different class.
    /// </summary>
    public bool UsesIrrelevantSendPort => false;

    /// <summary>
    /// Some poorly-behaved applications start advertising their OSCQuery endpoint long before they add /avatar/change
    /// endpoint to their tree. For those applications, we skip that check. 
    /// </summary>
    public bool AvatarChangeNodeAppearsWithDelay => false;
    
    /// <summary>
    /// Some platforms limit precision or range of parameters. This property can be used to provide default ranges.
    /// Note that OSCQuery-provided ranges would be used if specified.
    /// </summary>
    public (int Min, int Max)? IntegerParameterRange => null;
    public (float Min, float Max, float Step)? FloatParameterRange => null;
}
