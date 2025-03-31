using RmqConnection;

// TODO : identify PLCs IP address

public static class AppInfo
{
    public const string _appVersion = "1.0.0";
}

public class AbBlender : BackgroundService
{
    private readonly IRabbitMQConnectionManager _connectionManager;
    private readonly IPlcFinder _plcFinder;
    private readonly TagManager _tag_manager; // TODO : refactor into list AND make interfaces
    private readonly Queue<string> _outputs = [];

    public AbBlender(IRabbitMQConnectionManager connectionManager, IPlcFinder plcFinder)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _plcFinder = plcFinder ?? throw new ArgumentNullException(nameof(plcFinder));

        string? plc_address_ev = EnvVarHelper.GetPlcAddress();
        if (!string.IsNullOrEmpty(plc_address_ev))
        {
            _tag_manager = new TagManager(plc_address_ev, EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol());
        }
        else
        {
            _plcFinder.FindPlc(EnvVarHelper.GetStartIp(), EnvVarHelper.GetEndIp());
            string[] plc_ips = _plcFinder.GetPlcIps();
            if (plc_ips.Length == 0)
            {
                Console.WriteLine("No PLCs found from EnvVar nor from scanning; exiting");
                Environment.Exit(1);
            }
            // _tag_manager = new TagManager(_plcFinder.GetPlcIps()[0], EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol());
        }
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
