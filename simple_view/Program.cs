using System;
using libplctag;

class Program
{
    static void Main(string[] args)
    {
        var myTag = new Tag()
        {
            // Name = "foo",
            // Gateway = "192.168.130.121",
            // Path = "1,0",
            // PlcType = PlcType.Micro800,
            // // Protocol = Protocol.ab_eip
            // Protocol = Protocol.modbus_tcp

                Name = "foo",
                // Name = "__SYSVA_TYCOVERFLOW",
                Gateway = "192.168.130.121",
                PlcType = PlcType.Micro800,
                Protocol = Protocol.modbus_tcp
        };


        // Read the value from the PLC
        myTag.Read();
        int originalValue = myTag.GetInt32(0);
        Console.WriteLine($"Original value: {originalValue}");

        // Write a new value to the PLC
        int updatedValue = 1234;
        myTag.SetInt32(0, updatedValue);
        myTag.Write();
        Console.WriteLine($"Updated value: {updatedValue}");
    }
}