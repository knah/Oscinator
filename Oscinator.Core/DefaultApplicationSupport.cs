namespace Oscinator.Core;

internal class DefaultApplicationSupport : IApplicationSupport
{
    public int? MatchRemoteName(string name) => int.MinValue;
    internal static readonly DefaultApplicationSupport Instance = new();
}