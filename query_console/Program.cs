using System;
using libplctag;

class Program
{
    static void Main(string[] args)
    {
        var tags = new Tag<TagInfoPlcMapper, TagInfo[]>()
        {
            Gateway = "192.168.1.100", // Replace with the actual IP address of your PLC
            PlcType = PlcType.CompactLogix,
            Protocol = Protocol.ab_eip,
            Name = "@tags"
        };

        tags.Read();
        Console.WriteLine("All tags in the PLC:");
        foreach (var tag in tags.Value)
        {
            Console.WriteLine($"Name: {tag.Name}, Type: 0x{tag.Type:X}, Length: {tag.Length}");
        }
    }
}