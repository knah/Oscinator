using System.Diagnostics;
using System.Net;

namespace Oscinator.Core.PortDetection;

public interface IPlatformPortDetector
{
    Process? FindProcessOnEndpoint(IPEndPoint endPoint);
}