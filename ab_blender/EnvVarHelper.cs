using libplctag;

public static class EnvVarHelper
{
    public const string PLC_IP = "PLC_IP";
    public const string PLC_TYPE = "PLC_TYPE";
    public const string PLC_PROTOCOL = "PLC_PROTOCOL";
    public const string STUB_PLC = "STUB_PLC";
    public const string READ_TAGS_PERIOD_MS = "READ_TAGS_PERIOD_MS";
    public const string RABBITMQ_HOST = "RABBITMQ_HOST";
    public const string RABBITMQ_USER = "RABBITMQ_USER";
    public const string RABBITMQ_PASS = "RABBITMQ_PASS";
    public const string RABBITMQ_EXCHANGE = "RABBITMQ_EXCHANGE";
    public const string RABBITMQ_ROUTING_KEY = "RABBITMQ_ROUTING_KEY";
    public const string RABBITMQ_CONNECTION_NAME = "RABBITMQ_CONNECTION_NAME";
    public const string TAG_DEF_FILE = "TAG_DEF_FILE";
    public static int GetReadTagsPeriodMs()
    {
        int default_ms = 1000;
        string? read_tags_period_ms_s = Environment.GetEnvironmentVariable(READ_TAGS_PERIOD_MS);
        if (string.IsNullOrEmpty(read_tags_period_ms_s))
        {
            Console.WriteLine($"{READ_TAGS_PERIOD_MS} environment variable not set; exiting...");
            return default_ms;
        }
        if (!int.TryParse(read_tags_period_ms_s, out int read_tags_period_ms))
        {
            Console.WriteLine($"Invalid {READ_TAGS_PERIOD_MS} ; value : {read_tags_period_ms_s}");
            return default_ms;
        }
        return read_tags_period_ms;
    }
    public static string get_tag_def_filepath()
    {
        return Environment.GetEnvironmentVariable(TAG_DEF_FILE)!;
    }

    public static string GetPlcAddress()
    {
        string plc_address = Environment.GetEnvironmentVariable(PLC_IP)!;
        if (string.IsNullOrEmpty(plc_address))
        {
            Console.WriteLine($"{PLC_IP} environment variable not set; exiting...");
            Environment.Exit(1);
        }
        return plc_address;
    }
    public static Protocol GetPlcProtocol()
    {
        string? protocol_str = Environment.GetEnvironmentVariable(PLC_PROTOCOL);
        if (string.IsNullOrEmpty(protocol_str))
        {
            Console.WriteLine($"{PLC_PROTOCOL} environment variable not set; exiting...");
            Environment.Exit(1);
        }
        Dictionary<string, Protocol> protocol_rev = new()
        {
            { "ab_eip", Protocol.ab_eip },
            { "modbus_tcp", Protocol.modbus_tcp }
        };
        if (!protocol_rev.TryGetValue(protocol_str, out Protocol protocol))
        {
            Console.WriteLine($"Invalid {PLC_PROTOCOL} ; value : {protocol_str}, but valid values are {string.Join(", ", protocol_rev.Keys)}");
            Environment.Exit(1);
        }
        return protocol;
    }
    public static PlcType GetPlcType()
    {
        string? plc_str = Environment.GetEnvironmentVariable(PLC_TYPE);
        if (string.IsNullOrEmpty(plc_str))
        {
            Console.WriteLine($"{PLC_TYPE} environment variable not set; exiting...");
            Environment.Exit(1);
        }
        Dictionary<string, PlcType> plc_type_rev = new()
        {
            { "ControlLogix", PlcType.ControlLogix },
            { "Plc5", PlcType.Plc5 },
            { "Slc500", PlcType.Slc500 },
            { "LogixPccc", PlcType.LogixPccc },
            { "Micro800", PlcType.Micro800 },
            { "MicroLogix", PlcType.MicroLogix },
            { "Omron", PlcType.Omron }
        };
        if (!plc_type_rev.TryGetValue(plc_str, out PlcType plc_type))
        {
            Console.WriteLine($"Invalid {PLC_TYPE} ; value : {plc_str}, but valid values are {string.Join(", ", plc_type_rev.Keys)}");
            Environment.Exit(1);
        }
        return plc_type;
    }
    public static bool GetPlcStub()
    // TODO :  consider breaking env-var content out into a separate class for separation of concerns and better testing
    {
        string? stub_plc_s = Environment.GetEnvironmentVariable(STUB_PLC);
        if (!string.IsNullOrEmpty(stub_plc_s))
        {
            if (stub_plc_s == "true")
            {
                Console.WriteLine($"{STUB_PLC} set ; stubbing PLC interactions");
                return true;
            }
        }
        return false;
    }
}