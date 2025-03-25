using System.Text.Json;
using libplctag;
using RabbitMQ.Client;
using System.Timers;
using System.Text.Json.Nodes;

class Program
{
    private const string PLC_IP = "PLC_IP";
    private const string PLC_TYPE = "PLC_TYPE";
    private const string PLC_PROTOCOL = "PLC_PROTOCOL";
    private const string STUB_PLC = "STUB_PLC";
    private const string READ_TAGS_PERIOD_MS = "READ_TAGS_PERIOD_MS";
    private const string RABBITMQ_HOST = "RABBITMQ_HOST";
    private const string RABBITMQ_USER = "RABBITMQ_USER";
    private const string RABBITMQ_PASS = "RABBITMQ_PASS";
    private const string RABBITMQ_EXCHANGE = "RABBITMQ_EXCHANGE";
    private const string RABBITMQ_ROUTING_KEY = "RABBITMQ_ROUTING_KEY";
    private const string RABBITMQ_RECONNECTION_PERIOD_MS = "RABBITMQ_RECONNECTION_PERIOD_MS";
    private const string RABBITMQ_CONNECTION_NAME = "RABBITMQ_CONNECTION_NAME";

    private static List<TagDefinition> _tags = [];
    private static readonly Dictionary<string, Tag> _plcTags = [];
    private static IConnection? _rabbitConnection;
    private static IChannel? _rabbitChannel;
    private static System.Timers.Timer? _readTimer;
    private static System.Timers.Timer? _reconnectTimer;
    private static readonly string _appVersion = "1.0.0";
    private static string? plc_address;
    private static string? _rmq_exchange;
    private static string? _rmq_rk;
    private static readonly PlcType _plc_type = GetPlcType();
    private static readonly Protocol _plc_protocol = GetPlcProtocol();
    private static readonly bool _stub_plc = GetPlcStub();
    static async Task Main(string[] args)
    {
        // Load tags from JSON file
        LoadTagsFromJson();

        // Initialize PLC tags
        InitializePlcTags();

        plc_address = Environment.GetEnvironmentVariable(PLC_IP)!;
        if (string.IsNullOrEmpty(plc_address))
        {
            Console.WriteLine($"{PLC_IP} environment variable not set; exiting...");
            Environment.Exit(1);
        }
        Console.WriteLine($"plc_address : {plc_address}");

        // Setup tag reading timer
        double readPeriodMs = double.Parse(Environment.GetEnvironmentVariable(READ_TAGS_PERIOD_MS) ?? "1000");
        _readTimer = new System.Timers.Timer(readPeriodMs);
        _readTimer.Elapsed += async (s,e) => await ReadTags();
        _readTimer.AutoReset = true;

        // Setup RabbitMQ if environment variables are present
        if (HasRabbitMqConfig())
        {
            await SetupRabbitMq();
            double reconnectPeriodMs = double.Parse(Environment.GetEnvironmentVariable(RABBITMQ_RECONNECTION_PERIOD_MS) ?? "5000");
            _reconnectTimer = new System.Timers.Timer(reconnectPeriodMs);
            _reconnectTimer.Elapsed += async (s, e) => await ReconnectRabbitMq();
            _reconnectTimer.AutoReset = true;
        }

        // Start reading tags
        _readTimer.Start();
        Console.WriteLine("Started reading tags...");

        // Keep application running
        await Task.Delay(-1);
    }

    private static void LoadTagsFromJson()
    {
        string jsonContent = File.ReadAllText("tags.json");
        _tags = JsonSerializer.Deserialize<List<TagDefinition>>(jsonContent)
            ?? throw new Exception("Failed to load tags from tags.json");
    }

    private static void InitializePlcTags()
    {
        foreach (var tagDef in _tags)
        {
            var tag = new Tag()
            {
                Name = tagDef.Name,
                Path = tagDef.Path,
                Gateway = plc_address,
                PlcType = _plc_type,
                Protocol = _plc_protocol
            };
            if (!_stub_plc)
            {
                tag.Initialize();
            }
            _plcTags.Add(tagDef.Name!, tag);
        }
    }

    private static async Task ReadTags()
    {
        JsonNode data = new JsonObject
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["app_version"] = _appVersion,
            ["tags"] = new JsonObject()
        };

        var timestamp = DateTime.UtcNow;

        foreach (var tag in _tags)
        {
            if (!_stub_plc)
            {
                _plcTags[tag.Name!].Read();
            }
            Tag this_plc_tag = _plcTags[tag.Name!];
            switch (tag.DataType)
            {
                case "bool":
                    bool b = false;
                    try
                    {
                        b = this_plc_tag.GetBit(0); // TODO : this breaks in my testing
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ReadTags for tag '{tag.Name}', type '{tag.DataType}' : {ex.Message}");
                    }
                    data["tags"]![tag.Name!] = b;
                    break;
                case "int32":
                    int i32 = 0;
                    try
                    {
                        i32 = this_plc_tag.GetInt32(0);// TODO : this breaks in my testing
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ReadTags for tag '{tag.Name}', type '{tag.DataType}' : {ex.Message}");
                    }
                    data["tags"]![tag.Name!] = i32;
                    break;
                case "float32":
                    double f = 0.0;
                    try
                    {
                        f = this_plc_tag.GetFloat32(0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ReadTags for tag '{tag.Name}', type '{tag.DataType}' : {ex.Message}");
                    }
                    data["tags"]![tag.Name!] = f;
                    break;
                default:
                    Console.WriteLine($"Unknown type : {tag.DataType}");
                    break;
            }
        }

        string jsonMessage = data.ToJsonString();

        // Publish to RabbitMQ if configured
        if (_rabbitChannel?.IsOpen == true)
        {
            var body = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
            var props = new BasicProperties();
            await _rabbitChannel.BasicPublishAsync(
                exchange: _rmq_exchange!,
                        routingKey: _rmq_rk!,
                        mandatory: false,
                        basicProperties: props,
                        body: body);
        }
        else
        {
            Console.WriteLine($"jsonMessage : {jsonMessage}");
        }
    }

    private static bool HasRabbitMqConfig()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_HOST)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_USER)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_PASS)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_ROUTING_KEY)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_CONNECTION_NAME));
    }

    private static async Task SetupRabbitMq()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable(RABBITMQ_HOST)!,
                UserName = Environment.GetEnvironmentVariable(RABBITMQ_USER)!,
                Password = Environment.GetEnvironmentVariable(RABBITMQ_PASS)! ,
                AutomaticRecoveryEnabled = true
            };

            _rabbitConnection = await factory.CreateConnectionAsync();
            _rabbitChannel = await _rabbitConnection.CreateChannelAsync();
            _rmq_exchange = Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)!;
            await _rabbitChannel.ExchangeDeclareAsync(_rmq_exchange, "topic");
            _rmq_rk = Environment.GetEnvironmentVariable(RABBITMQ_ROUTING_KEY);

            Console.WriteLine("Connected to RabbitMQ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RabbitMQ connection failed: {ex.Message}");
            _reconnectTimer?.Start();
        }
    }

    private static async Task ReconnectRabbitMq()
    {
        if (_rabbitChannel?.IsOpen != true)
        {
            Console.WriteLine("Attempting to reconnect to RabbitMQ...");
            if (_rabbitConnection != null)
            {
                await _rabbitConnection.CloseAsync();
                await _rabbitConnection.DisposeAsync();
            }
            await SetupRabbitMq();
            if (_rabbitChannel?.IsOpen == true)
            {
                _reconnectTimer?.Stop();
                Console.WriteLine("RabbitMQ reconnected successfully");
            }
        }
    }

    private static bool GetPlcStub()
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

    private static PlcType GetPlcType()
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
    private static Protocol GetPlcProtocol()
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
}

class TagDefinition
{
    public string? Name { get; set; }
    public string? DataType { get; set; }
    public string? Path { get; set; }
}