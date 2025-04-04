using libplctag;

public static class EnvVarHelper
{
    private const string PLC_IP = "PLC_IP";
    private const string PLC_TYPE = "PLC_TYPE";
    private const string PLC_PROTOCOL = "PLC_PROTOCOL";
    private const string STUB_PLC = "STUB_PLC";
    private const string READ_MONITORED_TAGS_PERIOD_MS = "READ_MONITORED_TAGS_PERIOD_MS";

    private const string READ_ALL_TAGS_PERIOD_MS = "READ_ALL_TAGS_PERIOD_MS";
    private const string PUBLISH_MONITORED_TAG_PERIOD = "PUBLISH_MONITORED_TAG_PERIOD";
    private const string PUBLISH_ALL_TAG_PERIOD = "PUBLISH_ALL_TAG_PERIOD";
    private const string RABBITMQ_HOST = "RABBITMQ_HOST";
    private const string RABBITMQ_USER = "RABBITMQ_USER";
    private const string RABBITMQ_PASS = "RABBITMQ_PASS";
    private const string RABBITMQ_EXCHANGE = "RABBITMQ_EXCHANGE";
    private const string RABBITMQ_ROUTING_KEY = "RABBITMQ_ROUTING_KEY";
    private const string RABBITMQ_CONNECTION_NAME = "RABBITMQ_CONNECTION_NAME";
    private const string TAG_DEF_FILE = "TAG_DEF_FILE";
    private const string START_IP = "START_IP";
    private const string END_IP = "END_IP";
    public static string GetRmqHost()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_HOST)!;
    }
    public static string GetRmqUser()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_USER)!;
    }
    public static string GetRmqPass()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_PASS)!;
    }
    public static string GetRmqExchange()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)!;
    }
    public static string GetRmqRoutingKey()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_ROUTING_KEY)!;
    }
    public static string GetRmqConnectionName()
    {
        return Environment.GetEnvironmentVariable(RABBITMQ_CONNECTION_NAME)!;
    }
    public static int GetReadMonitoredTagsPeriodMs()
    {
        int default_ms = 1000;
        string? ev_s = Environment.GetEnvironmentVariable(READ_MONITORED_TAGS_PERIOD_MS);
        if (string.IsNullOrEmpty(ev_s))
        {
            Console.WriteLine($"{READ_MONITORED_TAGS_PERIOD_MS} environment variable not set; exiting...");
            return default_ms;
        }
        if (!int.TryParse(ev_s, out int output))
        {
            Console.WriteLine($"Invalid {READ_MONITORED_TAGS_PERIOD_MS} ; value : {ev_s}");
            return default_ms;
        }
        return output;
    }
    public static int GetReadAllTagsPeriodMs()
    {
        int default_ms = 1000;
        string? ev_s = Environment.GetEnvironmentVariable(READ_ALL_TAGS_PERIOD_MS);
        if (string.IsNullOrEmpty(ev_s))
        {
            Console.WriteLine($"{READ_ALL_TAGS_PERIOD_MS} environment variable not set; exiting...");
            return default_ms;
        }
        if (!int.TryParse(ev_s, out int output))
        {
            Console.WriteLine($"Invalid {READ_ALL_TAGS_PERIOD_MS} ; value : {ev_s}");
            return default_ms;
        }
        return output;
    }


    public static int GetPublishMonitoredTagsPeriodMs()
    {
        int default_ms = 1000;
        string? ev_s = Environment.GetEnvironmentVariable(PUBLISH_MONITORED_TAG_PERIOD);
        if (string.IsNullOrEmpty(ev_s))
        {
            Console.WriteLine($"{PUBLISH_MONITORED_TAG_PERIOD} environment variable not set; exiting...");
            return default_ms;
        }
        if (!int.TryParse(ev_s, out int output))
        {
            Console.WriteLine($"Invalid {PUBLISH_MONITORED_TAG_PERIOD} ; value : {ev_s}");
            return default_ms;
        }
        return output;
    }
    public static int GetPublishAllTagsPeriodMs()
    {
        int default_ms = 1000;
        string? ev_s = Environment.GetEnvironmentVariable(PUBLISH_ALL_TAG_PERIOD);
        if (string.IsNullOrEmpty(ev_s))
        {
            Console.WriteLine($"{PUBLISH_ALL_TAG_PERIOD} environment variable not set; exiting...");
            return default_ms;
        }
        if (!int.TryParse(ev_s, out int output))
        {
            Console.WriteLine($"Invalid {PUBLISH_ALL_TAG_PERIOD} ; value : {ev_s}");
            return default_ms;
        }
        return output;
    }
    public static string get_tag_def_filepath()
    {
        return Environment.GetEnvironmentVariable(TAG_DEF_FILE)!;
    }

    public static string? GetPlcAddress()
    {
        string? plc_address = Environment.GetEnvironmentVariable(PLC_IP)!;
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
    {
        string? stub_plc_s = Environment.GetEnvironmentVariable(STUB_PLC);
        if (!string.IsNullOrEmpty(stub_plc_s))
        {
            if (stub_plc_s == "true")
            {
                return true;
            }
        }
        return false;
    }
    public static string? GetStartIp()
    {
        string? start_ip = Environment.GetEnvironmentVariable(START_IP);
        return start_ip;
    }
    public static string? GetEndIp()
    {
        string? end_ip = Environment.GetEnvironmentVariable(END_IP);
        return end_ip;
    }
}