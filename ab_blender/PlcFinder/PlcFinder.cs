using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

// write a csharp application that will search a given range of ip addresses for a  connected Allen Bradley ControlLogix Plc
class PlcFinder
{
    public static string[] plc_ips = [];

    static async Task FindPlc(string startIp, string endIp)
    {
        Console.WriteLine("Allen-Bradley ControlLogix PLC Scanner");
        Console.WriteLine("-------------------------------------");

        Console.WriteLine($"Scanning range: {startIp} to {endIp}");
        await ScanIpRange(startIp, endIp);

        Console.WriteLine("\nScan complete. Press any key to exit.");
        Console.ReadKey();
    }

    static async Task ScanIpRange(string startIp, string endIp)
    {
        IPAddress start = IPAddress.Parse(startIp);
        IPAddress end = IPAddress.Parse(endIp);
        byte[] startBytes = start.GetAddressBytes();
        byte[] endBytes = end.GetAddressBytes();

        // Convert to long for easier iteration
        uint startNum = BitConverter.ToUInt32(startBytes, 0);
        uint endNum = BitConverter.ToUInt32(endBytes, 0);

        // Swap if start is greater than end
        if (startNum > endNum)
        {
            (startNum, endNum) = (endNum, startNum);
        }

        List<Task> tasks = new List<Task>();

        for (uint i = startNum; i <= endNum; i++)
        {
            byte[] ipBytes = BitConverter.GetBytes(i);
            Array.Reverse(ipBytes); // Convert back to correct byte order
            string ip = new IPAddress(ipBytes).ToString();

            tasks.Add(ScanDevice(ip));
        }

        await Task.WhenAll(tasks);
    }

    static async Task ScanDevice(string ip)
    {
        try
        {
            // First, ping the device
            using (Ping ping = new Ping())
            {
                PingReply reply = await ping.SendPingAsync(ip, 1000); // 1 second timeout
                if (reply.Status == IPStatus.Success)
                {
                    // If ping succeeds, try to connect to ControlLogix port
                    if (await CheckPlcPort(ip))
                    {
                        Console.WriteLine($"Potential PLC found at {ip} - Port 44818 open");
                        plc_ips.Append(ip);
                    }
                    else
                    {
                        Console.WriteLine($"Device responded at {ip}, but not a ControlLogix PLC");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning {ip}: {ex.Message}");
        }
    }

    static async Task<bool> CheckPlcPort(string ip, int port = 44818, int timeoutMs = 1000)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                // Set connection timeout
                Task connectTask = client.ConnectAsync(ip, port);
                Task timeoutTask = Task.Delay(timeoutMs);

                Task completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask && client.Connected)
                {
                    return true; // Port is open, likely a ControlLogix PLC
                }
                return false; // Timeout or connection failed
            }
        }
        catch
        {
            return false;
        }
    }
}