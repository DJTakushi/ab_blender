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
                    await _connectionManager.SetupConnectionsAsync();
                    publishOutputToRabbitMQ();
                }
                while (_outputs.Count > 0)
                {
                    Console.WriteLine($"{_outputs.Dequeue()}");
                }

                await Task.Delay(EnvVarHelper.GetReadTagsPeriodMs(), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteAsync: {ex.Message}");
            }
        }
    }

    private void publishOutputToRabbitMQ()
    {
        if (_connectionManager.IsOutputOpen())
        {
            while (_outputs.Count > 0)
            {
                _connectionManager.PublishOutputToRabbitMQ(_outputs.Dequeue());
            }
        }
    }

}
