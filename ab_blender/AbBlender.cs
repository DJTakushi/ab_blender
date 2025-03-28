using RabbitMQ.Client;
using RmqConnection;

// TODO : identify PLCs IP address

public static class AppInfo
{
    public const string _appVersion = "1.0.0";
}

public class AbBlender : BackgroundService
{
    private readonly IRabbitMQConnectionManager _connectionManager;
    private TagManager _tag_manager = new();
    private ConnectionFactory? _outputFactory = null;
    private IConnection? _outputConnection = null;
    private IChannel? _outputChannel = null; // TODO : consider breaking RMQ out into a separate class (better than current IRabbitMQConnectionManager)
    private static List<tag_attribute> attributes = [];
    private static string? _rmq_exchange;
    private static string? _rmq_rk;
    private static readonly bool _stub_plc = EnvVarHelper.GetPlcStub(); // TODO : use depencency inection instead of branches
    private readonly Queue<string> _outputs = [];

    public AbBlender(IRabbitMQConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _tag_manager.load_tags();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _outputs.Enqueue(_tag_manager.genTagTelemetry());
                if (HasRabbitMqConfig())
                {
                    SetupConnectionsAsync();
                    await publishOutputToRabbitMQ();
                }
                else
                {
                    while (_outputs.Count > 0)
                    {
                        Console.WriteLine($"{_outputs.Dequeue()}");
                    }
                }

                await Task.Delay(EnvVarHelper.GetReadTagsPeriodMs(), stoppingToken);
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
                string c_name = Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_CONNECTION_NAME)!;
                _outputConnection = await _connectionManager.CreateConnection(_outputFactory, c_name);
                _outputChannel = await _outputConnection.CreateChannelAsync();

                _rmq_exchange = Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_EXCHANGE)!;
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

    public static bool HasRabbitMqConfig()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_HOST)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_USER)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_PASS)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_EXCHANGE)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_ROUTING_KEY)) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_CONNECTION_NAME));
    }
    private async Task publishOutputToRabbitMQ()
    {
        _rmq_exchange = Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_EXCHANGE);
        _rmq_rk = Environment.GetEnvironmentVariable(EnvVarHelper.RABBITMQ_ROUTING_KEY);

        if (_outputChannel?.IsOpen == true)
        {
            while (_outputs.Count > 0)
            {
                var body = System.Text.Encoding.UTF8.GetBytes(_outputs.Dequeue());
                var props = new BasicProperties();
                await _outputChannel.BasicPublishAsync(
                    exchange: _rmq_exchange!,
                            routingKey: _rmq_rk!,
                            mandatory: false,
                            basicProperties: props,
                            body: body);
            }
        }
    }

}
