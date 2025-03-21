using System.Text.Json;
using libplctag;
using RabbitMQ.Client;
using System.Timers;
using System.Text.Json.Nodes;

class Program
{
    private static List<TagDefinition> _tags = new();
    private static Dictionary<string, Tag> _plcTags = new Dictionary<string, Tag>();
    private static HashSet<string> _printedTags = new();
    private static IConnection _rabbitConnection;
    private static IModel _rabbitChannel;
    private static System.Timers.Timer _readTimer;
    private static System.Timers.Timer _reconnectTimer;
    private static readonly string _appVersion = "1.0.0";
    private static string plc_address;
    static async Task Main(string[] args)
    {
        // Load tags from JSON file
        LoadTagsFromJson();

        // Initialize PLC tags
        InitializePlcTags();

        plc_address = Environment.GetEnvironmentVariable("PLC_IP");
        if (string.IsNullOrEmpty(plc_address))
        {
            Console.WriteLine($"PLC_IP environment variable not set; exiting...");
            Environment.Exit(1);
        }
        Console.WriteLine($"plc_address : {plc_address}");

        // Setup tag reading timer
        double readPeriodMs = double.Parse(Environment.GetEnvironmentVariable("READ_TAGS_PERIOD_MS") ?? "1000");
        _readTimer = new System.Timers.Timer(readPeriodMs);
        _readTimer.Elapsed += ReadTags;
        _readTimer.AutoReset = true;

        // Setup RabbitMQ if environment variables are present
        if (HasRabbitMqConfig())
        {
            await SetupRabbitMq();
            double reconnectPeriodMs = double.Parse(Environment.GetEnvironmentVariable("RABBITMQ_RECONNETION_PERIOD_MS") ?? "5000");
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
                PlcType = PlcType.ControlLogix,
                Protocol = Protocol.ab_eip
            };
            // tag.Initialize(); // TODO : init tags
            _plcTags.Add(tagDef.Name, tag);
        }
    }

    private static void ReadTags(object sender, ElapsedEventArgs e)
    {
        JsonNode data = new JsonObject();
        data["timestamp"] = DateTime.UtcNow.ToString("O");
        data["app_version"] = _appVersion;
        data["tags"] = new JsonObject();

        var timestamp = DateTime.UtcNow;

        foreach (var tag in _tags)
        {
            // data["tags"][tag.Name].Read(); TODO : activate this to read from PLC
            switch (tag.DataType)
            {
                case "bool":
                    bool b = false;
                    try
                    {
                        b = _plcTags[tag.Name].GetBit(0); // TODO : this breaks in my testing
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ReadTags for tag '{tag.Name}', type '{tag.DataType}' : {ex.Message}");
                    }
                    data["tags"][tag.Name] = b;
                    break;
                case "int32":
                    int i32 = 0;
                    try
                    {
                        i32 = _plcTags[tag.Name].GetInt32(0);// TODO : this breaks in my testing
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ReadTags for tag '{tag.Name}', type '{tag.DataType}' : {ex.Message}");
                    }
                    data["tags"][tag.Name] = i32;
                    break;
                case "float32":
                    data["tags"][tag.Name] = _plcTags[tag.Name].GetFloat32(0);
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
            _rabbitChannel.BasicPublish(
                exchange: Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE"),
                        routingKey: Environment.GetEnvironmentVariable("RABBITMQ_ROUTING_KEY"),
                        basicProperties: null,
                        body: body);
        }
        else
        {
            Console.WriteLine($"jsonMessage : {jsonMessage}");
        }
    }

    private static bool HasRabbitMqConfig()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RABBITMQ_HOST")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RABBITMQ_USER")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RABBITMQ_PASS")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RABBITMQ_ROUTING_KEY"));
    }

    private static async Task SetupRabbitMq()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER"),
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS"),
                AutomaticRecoveryEnabled = true
            };

            _rabbitConnection = factory.CreateConnection();
            _rabbitChannel = _rabbitConnection.CreateModel();
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
            _rabbitConnection?.Close();
            await SetupRabbitMq();
            if (_rabbitChannel?.IsOpen == true)
            {
                _reconnectTimer.Stop();
                Console.WriteLine("RabbitMQ reconnected successfully");
            }
        }
    }
}

class TagDefinition
{
    public string Name { get; set; }
    public string DataType { get; set; }
    public string Path { get; set; }
}