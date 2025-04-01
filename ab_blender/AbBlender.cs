using RmqConnection;

// TODO : identify PLCs IP address

public static class AppInfo
{
    public const string _appVersion = "1.0.0";
}

public class AbBlender : BackgroundService
{
    private readonly ITagAttributeFactory _tagFactory;
    private readonly IRabbitMQConnectionManager _connectionManager;
    private readonly IPlcFinder _plcFinder;
    private readonly Dictionary<string, PlcManager> _tag_managers = []; // TODO : make interfaces
    private readonly Queue<string> _outputs = [];

    public AbBlender(ITagAttributeFactory _tagFactory, IRabbitMQConnectionManager connectionManager, IPlcFinder plcFinder)
    {
        _tagFactory = _tagFactory ?? throw new ArgumentNullException(nameof(_tagFactory));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _plcFinder = plcFinder ?? throw new ArgumentNullException(nameof(plcFinder));

        string? plc_address_ev = EnvVarHelper.GetPlcAddress();
        if (!string.IsNullOrEmpty(plc_address_ev))
        {
            _tag_managers.Add(plc_address_ev, new PlcManager(_tagFactory, plc_address_ev, EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol()));
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
            foreach (string plc_ip in plc_ips)
            {
                _tag_managers.Add(plc_ip, new PlcManager(_tagFactory, plc_ip, EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol()));
            }
        }
        foreach (KeyValuePair<string, PlcManager> entry in _tag_managers)
        {
            entry.Value.load_tags();
            Console.WriteLine($"Monitoring {entry.Key}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (KeyValuePair<string, PlcManager> entry in _tag_managers)
                {
                    _outputs.Enqueue(entry.Value.genTagTelemetry());
                }
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
