namespace Oscinator.Core;

public class ChilloutVrApplicationSupport : IApplicationSupport
{
    public bool UsesIrrelevantSendPort => true;
    public bool AvatarChangeNodeAppearsWithDelay => true;

    public int? MatchRemoteName(string name)
    {
        if (name.StartsWith("chilloutvr-", StringComparison.InvariantCultureIgnoreCase))
            return IApplicationSupport.PerfectMatchWeight;
        if (name.Contains("chillout", StringComparison.InvariantCultureIgnoreCase))
            return IApplicationSupport.GoodMatchWeight;

        return null;
    }
}