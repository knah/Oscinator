namespace Oscinator.Core.VRChat;

public class VrChatApplicationSupport : IApplicationSupport
{
    // VRC allegedly fixed this
    /*public async Task<IDictionary<string, ParameterVariant>?> ReadStoredParameters(string avatarId)
    {
        var savedParams = await VrcConfigsJsonSupport.TryLoadSavedParams(avatarId);
        return BuildSavedParameterDictionary(savedParams);
    }
    
    private static Dictionary<string, ParameterVariant>? BuildSavedParameterDictionary(VrcConfigsJsonSupport.AvatarSaveStateJson? saveState)
    {
        if (saveState == null || saveState.Values.Count == 0) return null;
        return saveState.Values.ToDictionary(it => it.Name, it => new ParameterVariant(it.Value));
    }*/

    public int? MatchRemoteName(string name)
    {
        if (name.StartsWith("vrchat-client", StringComparison.InvariantCultureIgnoreCase))
            return IApplicationSupport.PerfectMatchWeight;
        if (name.Contains("vrchat", StringComparison.InvariantCultureIgnoreCase))
            return IApplicationSupport.GoodMatchWeight;
        
        return null;
    }

    public bool UsesIrrelevantSendPort => true;
    public bool AvatarChangeNodeAppearsWithDelay => true;

    public (int Min, int Max)? IntegerParameterRange => (-127, 255);
    public (float Min, float Max, float Step)? FloatParameterRange => (-2, 2, 0.05f);
}