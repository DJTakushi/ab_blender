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
                if (_connectionManager.IsConfigurable())
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
                _outputConnection = await _connectionManager.CreateConnection(_outputFactory, EnvVarHelper.GetRmqConnectionName());
                _outputChannel = await _outputConnection.CreateChannelAsync();

                await _outputChannel.ExchangeDeclareAsync(EnvVarHelper.GetRmqExchange(), "topic");

                Console.WriteLine($"rmq connection established.");
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

    private async Task publishOutputToRabbitMQ()
    {
        if (_outputChannel?.IsOpen == true)
        {
            while (_outputs.Count > 0)
            {
                var body = System.Text.Encoding.UTF8.GetBytes(_outputs.Dequeue());
                var props = new BasicProperties();
                await _outputChannel.BasicPublishAsync(
                    exchange: EnvVarHelper.GetRmqExchange()!,
                            routingKey: EnvVarHelper.GetRmqRoutingKey()!,
                            mandatory: false,
                            basicProperties: props,
                            body: body);
            }
        }
    }

}
