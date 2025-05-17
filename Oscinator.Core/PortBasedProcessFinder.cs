using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Oscinator.Core.PortDetection;

namespace Oscinator.Core;

public static class PortBasedProcessFinder
{
    private static readonly IPlatformPortDetector? PortDetector;

    static PortBasedProcessFinder()
    {
        PortDetector = OperatingSystem.IsWindows() ? new WindowsPlatformPortDetector() : OperatingSystem.IsLinux() ? new LinuxPlatformPortDetector() : null;
    }
    
    public static Process? FindLocalProcess(IPEndPoint endPoint)
    {
        if (PortDetector == null) return null;
        
        var localAddresses = NetworkInterface.GetAllNetworkInterfaces().SelectMany(nic =>
            nic.GetIPProperties().UnicastAddresses.Where(it => it.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(it => it.Address)).ToHashSet();

        if (!localAddresses.Contains(endPoint.Address)) return null;
        
        return PortDetector.FindProcessOnEndpoint(endPoint);
    }
}