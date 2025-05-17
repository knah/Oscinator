using System.Net;
using Microsoft.Extensions.Logging;
using NanoOsc;
using Vrc.OscQuery;
using Vrc.OscQuery.Zeroconf;

namespace Oscinator.Core;

public sealed class OscinatorListener : IDisposable
{
    public readonly IPAddress BindAddress;
    public const string OscinatorAppPrefix = "Oscinator";
    
    private readonly OscSocket myOscSocket;
    private readonly OscQueryService myService;

    private static readonly ILogger Logger = LogUtils.LoggerFor<OscinatorListener>();
    
    public event OscMessageHandler<IPEndPoint> MessageHandler
    {
        add => myOscSocket.OnMessage += value;
        remove => myOscSocket.OnMessage -= value;
    }

    public OscinatorListener(IPAddress bindAddress)
    {
        BindAddress = bindAddress;
        Discovery = new MeaModDiscovery(LogUtils.LoggerFor<MeaModDiscovery>());
        Discovery.OnAnyOscServiceRemoved += p =>
            Logger.LogInformation("Service '{Name}' of type {Type} at {Address}:{Port} left", p.Name, p.Type, p.Address, p.Port);
        Discovery.OnAnyOscServiceAdded += p => 
            Logger.LogInformation("Found service '{Name}' of type {Type} at {Address}:{Port}", p.Name, p.Type, p.Address, p.Port);
        
        var socketLogger = LogUtils.LoggerFor<OscSocket>();
        myOscSocket = new OscSocket(new IPEndPoint(bindAddress, 0), logger: socketLogger);
        myOscSocket.ReaderTask.NoAwait(socketLogger, $"Socket read loop for {bindAddress}:{myOscSocket.Port}");
        Logger.LogInformation("OSC socket listening on {Address}:{Port}", bindAddress, myOscSocket.Port);
        
        myService = new OscQueryService($"Oscinator-{Random.Shared.Next():X8}", bindAddress, Discovery, LogUtils.Factory)
            .AdvertiseOscService(myOscSocket.Port)
            .WithEndpoint(AvatarState.AvatarChangePrefixS, "s", Attributes.AccessValues.Write)
            .StartHttpServer();
        
        myService.RefreshServices();
    }

    public IDiscovery Discovery { get; }

    public async ValueTask<bool> Send(ReadOnlyMemory<byte> data, IPEndPoint destination)
    {
        return await myOscSocket.Send(data, destination) == data.Length;
    }

    public void Dispose()
    {
        myService.Dispose();
        myOscSocket.Dispose();
    }
}