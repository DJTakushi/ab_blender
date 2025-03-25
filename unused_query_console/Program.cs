using System.Text.Json;
using System.Timers;
using libplctag;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

class Program
{
    private static readonly HashSet<string> _knownTags = new();
    private static Tag[] _tags;
    private static System.Timers.Timer _readTimer;
    private static System.Timers.Timer _rabbitMQReconnectTimer;
    private static IConnection _rabbitMQConnection;
    private static IModel _rabbitMQChannel;
    private static readonly string _appVersion = "1.0.0";
    private static readonly object _lock = new();
    private static bool _isRabbitMQConnected = false;

    static async Task Main(string[] args)
    {
        try
        {
            // Get configuration from environment variables
            string plcIp = Environment.GetEnvironmentVariable("PLC_IP") ?? "192.168.1.100";
            int readPeriodMs = int.Parse(Environment.GetEnvironmentVariable("READ_TAGS_PERIOD_MS") ?? "1000");
            string tagList = Environment.GetEnvironmentVariable("PLC_TAGS") ?? "Tag1,Tag2,Tag3";
            int rabbitMQReconnectMs = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_RECONNECTION_PERIOD_MS") ?? "5000");

            // Initialize PLC tags
            InitializeTags(plcIp, tagList.Split(','));

            // Initialize RabbitMQ connection
            await InitializeRabbitMQAsync(rabbitMQReconnectMs);

            // Set up timer for periodic reading
            _readTimer = new System.Timers.Timer(readPeriodMs);
            _readTimer.Elapsed += async (s, e) => await ReadTagsAsync();
            _readTimer.AutoReset = true;
            _readTimer.Start();

            Console.WriteLine($"Application started. Reading tags every {readPeriodMs}ms");
            await Task.Delay(-1); // Keep application running
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in application: {ex.Message}");
        }
        finally
        {
            Cleanup();
        }
    }

    private static void InitializeTags(string plcIp, string[] tagNames)
    {
        _tags = tagNames.Select(tagName => new Tag
        {
            Gateway = plcIp,
            Path = "1,0", // CPU slot 0
            PlcType = PlcType.ControlLogix,
            Protocol = Protocol.ab_eip,
            Name = tagName,
            Timeout = TimeSpan.FromSeconds(5)
        }).ToArray();

        foreach (var tag in _tags)
        {
            tag.Initialize();
        }
    }

    private static async Task InitializeRabbitMQAsync(int reconnectPeriodMs)
    {
        string host = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
        string user = Environment.GetEnvironmentVariable("RABBITMQ_USER");
        string pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS");
        string exchange = Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE");
        string routingKey = Environment.GetEnvironmentVariable("RABBITMQ_ROUTING_KEY");

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) ||
            string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(exchange) ||
            string.IsNullOrEmpty(routingKey))
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass,
            AutomaticRecoveryEnabled = false // We'll handle reconnection manually
        };

        // Set up reconnection timer
        _rabbitMQReconnectTimer = new System.Timers.Timer(reconnectPeriodMs);
        _rabbitMQReconnectTimer.Elapsed += async (s, e) => await TryReconnectRabbitMQAsync(factory, exchange);
        _rabbitMQReconnectTimer.AutoReset = true;

        // Initial connection attempt
        await TryReconnectRabbitMQAsync(factory, exchange);
    }

    private static async Task TryReconnectRabbitMQAsync(ConnectionFactory factory, string exchange)
    {
        try
        {
            if (_isRabbitMQConnected) return;

            _rabbitMQConnection?.Dispose();
            _rabbitMQChannel?.Dispose();

            _rabbitMQConnection = factory.CreateConnection();
            _rabbitMQChannel = _rabbitMQConnection.CreateModel();
            _rabbitMQChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true);

            _isRabbitMQConnected = true;
            _rabbitMQReconnectTimer.Stop();
            Console.WriteLine("RabbitMQ connection established");

            // Set up callback for connection shutdown
            _rabbitMQConnection.ConnectionShutdown += (s, e) =>
            {
                _isRabbitMQConnected = false;
                _rabbitMQReconnectTimer.Start();
                Console.WriteLine("RabbitMQ connection lost. Attempting to reconnect...");
            };
        }
        catch (Exception ex)
        {
            _isRabbitMQConnected = false;
            _rabbitMQReconnectTimer.Start();
            Console.WriteLine($"RabbitMQ connection failed: {ex.Message}");
        }
    }

    private static async Task ReadTagsAsync()
    {
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.UtcNow;
                var tagData = new Dictionary<string, object>();

                Console.WriteLine($"_tags.Length : {_tags.Length}");
                if (_tags.Length > 0)
                {

                    foreach (var tag in _tags)
                    {
                        tag.Read();

                        object value = tag.ElementType switch
                        {
                //             TagDataType.INT => tag.GetInt16(0),
                //             TagDataType.DINT => tag.GetInt32(0),
                //             TagDataType.REAL => tag.GetFloat32(0),
                //             _ => tag.GetString(0)
                        };

                //         if (_knownTags.Add(tag.Name))
                //         {
                //             Console.WriteLine($"New tag discovered: {tag.Name} = {value}");
                //         }

                //         tagData[tag.Name] = value;
                    }
                }

                if (_isRabbitMQConnected && _rabbitMQChannel != null)
                {
                    try
                    {
                        var message = new
                        {
                            Tags = tagData,
                            Timestamp = timestamp.ToString("O"),
                            Version = _appVersion
                        };

                        string json = JsonSerializer.Serialize(message);
                        var body = System.Text.Encoding.UTF8.GetBytes(json);

                        _rabbitMQChannel.BasicPublish(
                            exchange: Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE"),
                            routingKey: Environment.GetEnvironmentVariable("RABBITMQ_ROUTING_KEY"),
                            basicProperties: null,
                            body: body);
                    }
                    catch (Exception ex)
                    {
                        _isRabbitMQConnected = false;
                        _rabbitMQReconnectTimer.Start();
                        Console.WriteLine($"RabbitMQ publish failed: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading tags: {ex.Message}");
        }
    }

    private static void Cleanup()
    {
        _readTimer?.Dispose();
        _rabbitMQReconnectTimer?.Dispose();
        if (_tags != null)
        {
            foreach (var tag in _tags)
            {
                tag.Dispose();
            }
        }
        _rabbitMQChannel?.Dispose();
        _rabbitMQConnection?.Dispose();
    }
}