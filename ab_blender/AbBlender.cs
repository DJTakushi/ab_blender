using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection.Metadata;
using libplctag;
using RabbitMQ.Client;
using RmqConnection;


public class AbBlender : BackgroundService
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
    private const string RABBITMQ_CONNECTION_NAME = "RABBITMQ_CONNECTION_NAME";

    private readonly IRabbitMQConnectionManager _connectionManager;
    private ConnectionFactory? _outputFactory = null;
    private IConnection? _outputConnection = null;
    private IChannel? _outputChannel = null;

    private static List<TagDefinition> _tags = [];
    private static readonly Dictionary<string, Tag> _plcTags = [];
    private static readonly string _appVersion = "1.0.0";
    private static string? plc_address;
    private static string? _rmq_exchange;
    private static string? _rmq_rk;
    private static readonly PlcType _plc_type = GetPlcType();
    private static readonly Protocol _plc_protocol = GetPlcProtocol();
    private static readonly bool _stub_plc = GetPlcStub();

    public AbBlender(IRabbitMQConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

        LoadTagsFromJson();

        InitializePlcTags();

        plc_address = Environment.GetEnvironmentVariable(PLC_IP)!;
        if (string.IsNullOrEmpty(plc_address))
        {
            Console.WriteLine($"{PLC_IP} environment variable not set; exiting...");
            Environment.Exit(1);
        }
        Console.WriteLine($"plc_address : {plc_address}");

        // Setup RabbitMQ if environment variables are present
        if (HasRabbitMqConfig())
        {
            _rmq_exchange = Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)!;
            _rmq_rk = Environment.GetEnvironmentVariable(RABBITMQ_ROUTING_KEY);
        }
        else
        {
            Console.WriteLine("RabbitMQ configuration not found; skipping RabbitMQ setup.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (HasRabbitMqConfig())
                {
                    SetupConnectionsAsync();
                }
                await ReadTags();  // TODO : break into separate components

                int readPeriodMs = int.Parse(Environment.GetEnvironmentVariable(READ_TAGS_PERIOD_MS) ?? "1000");
                await Task.Delay(readPeriodMs, stoppingToken);
            }  
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteAsync: {ex.Message}");
            }
        }
    }

    internal virtual async void SetupConnectionsAsync()
    {
        try
        {
            if (_outputConnection?.IsOpen != true)
            {
                if (_outputConnection != null)
                {
                    await _outputConnection.CloseAsync();
                    await _outputConnection.DisposeAsync();
                }
                _outputConnection = null;

                _outputFactory = _connectionManager.CreateFactory(""); // TODO : use "OUTPUT_"
                _outputConnection = await _connectionManager.CreateConnection(_outputFactory, "ab_blender_output");
                _outputChannel = await _outputConnection.CreateChannelAsync();

                _rmq_exchange = Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)!;
                await _outputChannel.ExchangeDeclareAsync(_rmq_exchange, "topic");

                Console.WriteLine($"{_rmq_exchange} exchange created; rmq connection established.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetupConnectionsAsync: {ex.Message}");
            _outputFactory = null;
            _outputConnection = null;
            _outputChannel = null;
        }
    }

    private static void LoadTagsFromJson()
    {
        string jsonContent = File.ReadAllText("tags.json");
        _tags = JsonSerializer.Deserialize<List<TagDefinition>>(jsonContent)
            ?? throw new Exception("Failed to load tags from tags.json");
    }
    public static bool HasRabbitMqConfig()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_HOST)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_USER)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_PASS)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_EXCHANGE)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_ROUTING_KEY)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RABBITMQ_CONNECTION_NAME));
    }
    private async Task ReadTags()
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
        if (_outputChannel?.IsOpen == true)
        {
            var body = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
            var props = new BasicProperties();
            await _outputChannel.BasicPublishAsync(
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