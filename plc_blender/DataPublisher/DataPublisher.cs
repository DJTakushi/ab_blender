using RmqConnection;

public class DataPublisher : BackgroundService
{
    private readonly PlcBlender _PlcBlender;
    private readonly IRabbitMQConnectionManager _connectionManager;

    public DataPublisher(PlcBlender plcBlender, IRabbitMQConnectionManager connectionManager)
    {
        _PlcBlender = plcBlender ?? throw new ArgumentNullException(nameof(plcBlender));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Queue<string> outputs = _PlcBlender.GetTelemetryMessages();
                if (_connectionManager.IsConfigurable())
                {
                    await _connectionManager.SetupConnectionsAsync();
                }

                foreach (string output in outputs)
                {
                    if (_connectionManager.IsOutputOpen())
                    {
                        _connectionManager.PublishOutputToRabbitMQ(output);
                    }
                    else
                    {
                        Console.WriteLine(output);
                    }
                }
                await Task.Delay(EnvVarHelper.GetPublishMonitoredTagsPeriodMs(), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteAsync: {ex.Message}");
            }
        }
    }
}