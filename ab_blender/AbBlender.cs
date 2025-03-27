using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection.Metadata;
using libplctag;
using libplctag.DataTypes;
using RabbitMQ.Client;
using RmqConnection;

public enum tagType
{
    BOOL,
    INT,
    DINT = 196,
    REAL = 202,
    STRING
}
class tag_attribute
{
    public Tag tag { get; set; }
    public TagInfo tag_info { get; set; }
}
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
    private static List<tag_attribute> attributes = [];
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

        // InitializePlcTags();

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

        //process tags
        // JObject json = JObject.Parse(str);
        var jsonObj = JsonObject.Parse(jsonContent);
        foreach (var data in jsonObj.AsArray())
        {
            ushort type_t = 0;
            switch (data["DataType"].ToString())
            {
                case "BOOL":
                    type_t = (ushort)tagType.BOOL;
                    break;
                case "INT":
                    type_t = (ushort)tagType.INT;
                    break;
                case "DINT":
                    type_t = (ushort)tagType.DINT;
                    break;
                case "REAL":
                    type_t = (ushort)tagType.REAL;
                    break;
                case "STRING":
                    type_t = (ushort)tagType.STRING;
                    break;
                default:
                    Console.WriteLine($"Unknown type : {data["data_type"]}");
                    break;
            }

            attributes.Add(new tag_attribute
            {
                tag = new Tag
                {
                    Name = data["Name"].ToString(),
                    Path = data["Path"].ToString(), // WARNING :  https://github.com/libplctag/libplctag/wiki/Tag-String-Attributes
                    Gateway = plc_address,
                    PlcType = _plc_type,
                    Protocol = _plc_protocol
                },
                tag_info = new TagInfo
                {
                    Name = data["Name"].ToString(),
                    Type = type_t
                }
            });
            if (!_stub_plc)
            {
                attributes.Last().tag.Initialize();
            }
        }
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

        foreach (var attr in attributes)
        {
            if (!_stub_plc)
            {
                attr.tag.Read();
            }
            try
            {
                switch (attr.tag_info.Type)  // TODO ; reaplce this wiht deprecated numeric type (TagInfo.Type)
                {
                    case (ushort)tagType.BOOL:
                        break; // TODO : this breaks in my testing
                        data["tags"]![attr.tag_info.Name] = attr.tag.GetBit(0);
                        break;
                    case (ushort)tagType.INT:
                        break; // TODO : this breaks in my testing
                        data["tags"]![attr.tag_info.Name] = attr.tag.GetInt16(0);
                        break;
                    case (ushort)tagType.DINT:
                        break; // TODO : this breaks in my testing
                        data["tags"]![attr.tag_info.Name] = attr.tag.GetInt32(0);
                        break;
                    case (ushort)tagType.REAL:
                        data["tags"]![attr.tag_info.Name] = attr.tag.GetFloat32(0);
                        break;
                    case (ushort)tagType.STRING:
                        data["tags"]![attr.tag_info.Name] = attr.tag.GetString(0);
                        break;
                    default:
                        Console.WriteLine($"Unknown type : {attr.tag_info.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadTags for tag '{attr.tag_info.Name}', type '{attr.tag_info.Type}' : {ex.Message}");
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

    // private void getPlcTagsFromMapper()
    // {  // TODO : create an enum for types that matches mapper.
    //     var tags = new Tag<TagInfoPlcMapper, TagInfo[]>()  // OBSOLETE?!?
    //     {
    //         Gateway = plc_address,
    //         Path = "1,0",  // TODO ; un-hardcode this
    //         PlcType = PlcType.ControlLogix,
    //         Protocol = Protocol.ab_eip,
    //         Name = "@tags"
    //     };

    //     tags.Read();
    //     Console.WriteLine($"{tags.Value}");
    //     foreach (var tag in tags.Value)
    //     {

    //         //     ExampleRW.Run( tag.Name, PlcType.Micro800 , Protocol.ab_eip);
    //         var myTag = new Tag()
    //         {
    //             Name = $"{tag.Name}",
    //             Gateway = "172.16.31.2",
    //             Path = "1,0",
    //             PlcType = PlcType.ControlLogix,
    //             Protocol = Protocol.ab_eip
    //         };
    //         Console.WriteLine($"tag: {tag.Name} Time: {DateTime.Now} value: {myTag.GetFloat32(0)}");
    //     }
    // }
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
