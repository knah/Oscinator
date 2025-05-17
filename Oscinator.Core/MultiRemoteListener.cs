using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using NanoOsc;
using Oscinator.Core.VRChat;
using Vrc.OscQuery;

namespace Oscinator.Core;

public sealed class MultiRemoteListener : IDisposable
{
    private readonly ConcurrentDictionary<string, AvatarState> myRemoteStates = new();
    private readonly ConcurrentDictionary<IPEndPoint, string> myIpInverseMap = new();
    private readonly ConcurrentDictionary<string, Process> myRemoteProcessMap = new();
    private readonly CancellationTokenSource myCancellationTokenSource = new();
    private readonly MruCache<string> myUnknownNameCache = new(TimeSpan.FromMinutes(5));
    public readonly OscinatorListener Listener;

    private const string UnknownNamePrefix = "??? ";

    public event Action? OnRemoteSetChanged;

    public MultiRemoteListener(OscinatorListener listener)
    {
        Listener = listener;
        Listener.Discovery.OnAnyOscServiceRemoved += OnServiceRemoved;
        Listener.Discovery.OnAnyOscServiceAdded += OnServiceAdded;
        Listener.MessageHandler += OnOscMessage;

        myUnknownNameCache.OnExpiry += RemoveNamedService;
        myUnknownNameCache.StartExpiryChecking(Logger, myCancellationTokenSource.Token).NoAwait(Logger, "Expiry checker");
    }

    public IEnumerable<AvatarState> AvatarStates => myRemoteStates.Values;

    public string? GetProcessInfoForState(string name)
    {
        return myRemoteProcessMap.TryGetValue(name, out var processInfo) ? $"{processInfo.ProcessName} ({processInfo.Id})" : null;
    }

    private async Task InterrogateOscQueryService(OscQueryServiceProfile profile)
    {
        var serviceEndpoint = profile.EndPoint;
        
        var hostInfo = await Utils.GetHostInfo(serviceEndpoint.Address, serviceEndpoint.Port, myCancellationTokenSource.Token);
        if (hostInfo == null)
        {
            Logger.LogInformation("Can't get OSC host info from service {Name} at {Endpoint}", profile.Name, serviceEndpoint);
            return;
        }
        
        if (hostInfo.OscTransport != "UDP")
        {
            Logger.LogInformation("OscQuery service {Name} at {EP} uses unsupported transport type: {Transport}",
                hostInfo.Name, serviceEndpoint, hostInfo.OscTransport);
            return;
        }
        
        var remoteSupport = GetBestRemoteSupportByName(hostInfo.Name);
        if (!remoteSupport.AvatarChangeNodeAppearsWithDelay)
        {
            var tree = await Utils.GetOscTree(serviceEndpoint.Address, serviceEndpoint.Port, cancellationToken: myCancellationTokenSource.Token);
            if (tree == null)
            {
                Logger.LogInformation("Can't get OSC tree from service {Name} at {Endpoint}", hostInfo.Name,
                    serviceEndpoint);
                return;
            }
            
            var avatarChangeNode = tree.GetNodeWithPath("/avatar/change");

            if (avatarChangeNode == null)
            {
                Logger.LogInformation("OSC service {Name} at {Endpoint} contains no /avatar/change endpoint", hostInfo.Name, serviceEndpoint);
                return;
            }

            if ((avatarChangeNode.Access & Attributes.AccessValues.Read) == 0)
            {
                Logger.LogInformation("OSC service {Name} at {Endpoint} contains a non-readable /avatar/change endpoint", hostInfo.Name, serviceEndpoint);
                return;
            }
        }

        if (myRemoteStates.TryGetValue(hostInfo.Name, out var existingState) && existingState.ServiceProfile != null)
            return;

        var remoteProcess = PortBasedProcessFinder.FindLocalProcess(hostInfo.OscEndPoint);
        
        Logger.LogInformation("Will add OSC service {Name} at {Endpoint} with UDP endpoint {UdpEndpoint}, type {Type}, process {Process} ({ProcessId})",
            hostInfo.Name, serviceEndpoint, hostInfo.OscEndPoint, remoteSupport.GetType().Name, remoteProcess?.ProcessName, remoteProcess?.Id);
        myIpInverseMap[hostInfo.OscEndPoint] = hostInfo.Name;
        if (remoteProcess != null) myRemoteProcessMap[hostInfo.Name] = remoteProcess;
        var newState = new AvatarState(remoteSupport, hostInfo, profile);
        myRemoteStates[hostInfo.Name] = newState;
        
        OnRemoteSetChanged?.Invoke();
        
        newState.FetchCurrentAvatarId();
    }

    private void RemoveNamedService(string name)
    {
        if (!myRemoteStates.TryRemove(name, out var removed)) return;
        
        var profile = removed.HostInfo.OscEndPoint;
        myIpInverseMap.TryRemove(new IPEndPoint(profile.Address, profile.Port), out _);
        myRemoteProcessMap.TryRemove(name, out _);
        
        OnRemoteSetChanged?.Invoke();
    }

    private string GetRemoteServiceNameForSourceIp(IPEndPoint source)
    {
        return myIpInverseMap.GetOrAdd(source, static (point, arg) =>
        {
            var (states, processes) = arg;
            
            var remoteProcess = PortBasedProcessFinder.FindLocalProcess(point);
            if (remoteProcess != null)
                foreach (var (key, value) in processes)
                    if (value == remoteProcess)
                        return key;
            
            foreach (var (key, value) in states)
                if (value.ApplicationSupport.UsesIrrelevantSendPort)
                {
                    if (Equals(value.HostInfo.OscEndPoint.Address, point.Address))
                        return key;
                }
                else
                {
                    if (Equals(value.HostInfo.OscEndPoint, point))
                        return key;
                }

            return $"{UnknownNamePrefix}<{point};{remoteProcess}>";
        }, (myRemoteStates, myRemoteProcessMap));
    }

    private AvatarState GetStateForService(string serviceName, IPEndPoint remoteEndpoint)
    {
        if (myRemoteStates.TryGetValue(serviceName, out var result)) return result;
        
        result = myRemoteStates.GetOrAdd(serviceName, static (s, arg) => new AvatarState(GetBestRemoteSupportByName(s), new HostInfo()
        {
            Name = s,
            OscIp = arg.Address.ToString(),
            OscPort = arg.Port
        }, null), remoteEndpoint);
        OnRemoteSetChanged?.Invoke();
        return result;
    }

    private static readonly IApplicationSupport[] KnownSupports = [new VrChatApplicationSupport()];
    private static readonly ILogger<MultiRemoteListener> Logger = LogUtils.LoggerFor<MultiRemoteListener>();

    private static IApplicationSupport GetBestRemoteSupportByName(string serviceName)
    {
        return KnownSupports.MaxBy(it => it.MatchRemoteName(serviceName)) ?? DefaultApplicationSupport.Instance;
    }

    private void OnOscMessage(OscMessageParser parser, IPEndPoint source)
    {
        var remoteService = GetRemoteServiceNameForSourceIp(source);
        var remoteState = GetStateForService(remoteService, source);
        if (remoteService.StartsWith(UnknownNamePrefix))
            myUnknownNameCache.AddOrTouch(remoteService);
        remoteState.OnOscMessage(parser);
    }

    private void OnServiceRemoved(OscQueryServiceProfile profile)
    {
        if (profile.Type != OscQueryServiceProfile.ServiceType.OscQuery) return;
        
        myIpInverseMap.TryRemove(new IPEndPoint(profile.Address, profile.Port), out _);
        myRemoteStates.TryRemove(profile.Name, out _);
        OnRemoteSetChanged?.Invoke();
    }

    private void OnServiceAdded(OscQueryServiceProfile profile)
    {
        if (profile.Type != OscQueryServiceProfile.ServiceType.OscQuery) return;
        if (profile.Name.StartsWith(OscinatorListener.OscinatorAppPrefix)) return;
        
        InterrogateOscQueryService(profile).NoAwait(Logger, "OSCQuery service check");
    }

    public void Dispose()
    {
        myCancellationTokenSource.Dispose();
        Listener.Dispose();
    }
}