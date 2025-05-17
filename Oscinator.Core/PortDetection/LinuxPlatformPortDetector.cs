using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Oscinator.Core.PortDetection;

[SupportedOSPlatform("linux")]
public unsafe class LinuxPlatformPortDetector : IPlatformPortDetector
{
    public Process? FindProcessOnEndpoint(IPEndPoint endPoint)
    {
        try
        {
            var candidateInodes = new HashSet<string>();
            
            var expectedAddressSpecific = $"{BitConverter.ToUInt32(endPoint.Address.GetAddressBytes()):X08}:{endPoint.Port:X04}";
            var expectedAddressAny = $"00000000:{endPoint.Port:X04}";
            var lines = File.ReadLines("/proc/net/udp").Skip(1);
            foreach (var line in lines)
            {
                var split = line.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (split[1] != expectedAddressAny && split[1] != expectedAddressSpecific || !int.TryParse(split[3], out var state) || state != 7) continue;
                candidateInodes.Add($"socket:[{split[9]}]");
            }
            
            if (candidateInodes.Count == 0) 
                return null;
            
            using var readLinkBuffer = MemoryPool<byte>.Shared.Rent(512);

            foreach (var processDir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(processDir), out var pid)) continue;

                try
                {
                    foreach (var fd in Directory.EnumerateFileSystemEntries(Path.Combine(processDir, "fd")))
                        try
                        {
                            IntPtr readLinkResult;
                            fixed (byte* buffer = readLinkBuffer.Memory.Span)
                                readLinkResult = ReadLink(fd, buffer, readLinkBuffer.Memory.Length);

                            if (readLinkResult <= 0) continue;
                            
                            var returnedString = Encoding.UTF8.GetString(readLinkBuffer.Memory.Span[..(int)readLinkResult]);

                            if (!candidateInodes.Contains(returnedString)) continue;
                            
                            return Process.GetProcessById(pid);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            continue; // no-op; some descriptors may be inaccessible
                        }
                }
                catch (UnauthorizedAccessException)
                {
                    continue; // no-op; some processes may be inaccessible
                }
            }
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "readlink")]
    private static extern IntPtr ReadLink([MarshalAs(UnmanagedType.LPUTF8Str), In] string path, byte* buffer, int bufferLength);
}