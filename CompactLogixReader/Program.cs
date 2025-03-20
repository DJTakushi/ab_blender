using System.Text.Json;
using LibplctagWrapper;
using RabbitMQ.Client;
using System.Timers;

namespace CompactLogixReader
{
    class Program
    {
        private static List<TagDefinition> _tags = new();
        private static List<Tag> _plcTags = new();
        private static HashSet<string> _printedTags = new();
        private static IConnection _rabbitConnection;
        private static IModel _rabbitChannel;
        private static System.Timers.Timer _readTimer;
        private static System.Timers.Timer _reconnectTimer;
        private static readonly string _appVersion = "1.0.0";

        static async Task Main(string[] args)
        {
            // Load tags from JSON file
            LoadTagsFromJson();

            // Initialize PLC tags
            InitializePlcTags();

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
                var tag = new Tag(
                    ipAddress: tagDef.Path.Split(',')[0],
                    path: tagDef.Path.Substring(tagDef.Path.IndexOf(',') + 1),
                    cpuType: CpuType.LGX,
                    name: tagDef.Name,
                    dataType: ConvertToLibPlcTagDataType(tagDef.DataType),
                    elementCount: 1
                );
                _plcTags.Add(tag);
            }
        }

        private static void ReadTags(object sender, ElapsedEventArgs e)
        {
            var timestamp = DateTime.UtcNow;
            var readings = new Dictionary<string, object>();

            foreach (var tag in _plcTags)
            {
                int result = tag.Read(5000);
                if (result == Libplctag.PLCTAG_STATUS_OK)
                {
                    object value = tag.DataType switch
                    {
                        DataType.Bool => tag.GetBitValue(0),
                        DataType.Int16 => tag.GetInt16Value(0),
                        DataType.Int32 => tag.GetInt32Value(0),
                        DataType.Float32 => tag.GetFloat32Value(0),
                        _ => throw new NotSupportedException($"Unsupported data type: {tag.DataType}")
                    };

                    readings[tag.Name] = value;

                    // Print tag first time it's read
                    if (_printedTags.Add(tag.Name))
                    {
                        Console.WriteLine($"First read - {tag.Name}: {value}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error reading tag {tag.Name}: {Libplctag.DecodeError(result)}");
                }
            }

            // Publish to RabbitMQ if configured
            if (_rabbitChannel?.IsOpen == true)
            {
                var message = new
                {
                    Timestamp = timestamp.ToString("O"),
                    Version = _appVersion,
                    Tags = readings
                };
                string jsonMessage = JsonSerializer.Serialize(message);
                var body = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
                
                _rabbitChannel.BasicPublish(
                    exchange: Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE"),
                    routingKey: Environment.GetEnvironmentVariable("RABBITMQ_ROUTING_KEY"),
                    basicProperties: null,
                    body: body);
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

        private static DataType ConvertToLibPlcTagDataType(string dataType)
        {
            return dataType.ToLower() switch
            {
                "bool" => DataType.Bool,
                "int16" => DataType.Int16,
                "int32" => DataType.Int32,
                "float32" => DataType.Float32,
                _ => throw new NotSupportedException($"Unsupported data type: {dataType}")
            };
        }
    }

    class TagDefinition
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Path { get; set; }
    }
}