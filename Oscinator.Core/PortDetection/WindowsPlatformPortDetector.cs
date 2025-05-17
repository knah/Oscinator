using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Oscinator.Core.PortDetection;

[SupportedOSPlatform("windows")]
public unsafe class WindowsPlatformPortDetector : IPlatformPortDetector
{
    public Process? FindProcessOnEndpoint(IPEndPoint endPoint)
    {
        var tableSize = 0;
        
        while (true)
        {
            var currentTableSize = tableSize;
            using var tableBuffer = MemoryPool<byte>.Shared.Rent(currentTableSize);
            currentTableSize = Math.Min(currentTableSize, tableBuffer.Memory.Length);
            IntPtr result;
            fixed (void* buffer = tableBuffer.Memory.Span)
                result = GetExtendedUdpTable(buffer, ref currentTableSize, 0, (int)AddressFamily.InterNetwork, UDP_TABLE_OWNER_PID, 0);

            if (result == ERROR_INSUFFICIENT_BUFFER)
            {
                tableSize = currentTableSize + 16;
                continue;
            }

            if (result == IntPtr.Zero)
            {
                var entriesOffset = (int)Marshal.OffsetOf<UdpTable>(nameof(UdpTable.FirstEntry));
                var numEntries = MemoryMarshal.Cast<byte, int>(tableBuffer.Memory.Span)[0];
                var entriesSpan = MemoryMarshal.Cast<byte, ExtendedUdpTableEntry>(tableBuffer.Memory.Span[entriesOffset..])[..numEntries];
                return FindProcessOnEndpoint(endPoint, entriesSpan);
            }

            throw new Exception($"Unexpected error ({result}) from {nameof(GetExtendedUdpTable)}");
        }
    }

    private static Process? FindProcessOnEndpoint(IPEndPoint endPoint, Span<ExtendedUdpTableEntry> entriesSpan)
    {
        var expectedNumericalIp = BitConverter.ToInt32(endPoint.Address.GetAddressBytes());
        var expectedPort = BinaryPrimitives.ReverseEndianness((ushort)endPoint.Port);
        foreach (var entry in entriesSpan)
            if ((entry.Address == expectedNumericalIp || entry.Address == 0) && entry.Port == expectedPort && entry.Pid != 0)
                return Process.GetProcessById(entry.Pid);

        return null;
    }

    [DllImport("Iphlpapi.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr GetExtendedUdpTable(void* table, ref int tableSize, int order, int addressFamily, int tableClass, int reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedUdpTableEntry // MIB_UDPROW_OWNER_PID
    {
        public int Address;
        public int Port;
        public int Pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UdpTable // MIB_UDPTABLE_OWNER_PID
    {
        public int NumEntries;
        public ExtendedUdpTableEntry FirstEntry;
    }

    private const int UDP_TABLE_OWNER_PID = 1;
    private const IntPtr ERROR_INSUFFICIENT_BUFFER = 0x7a;
}