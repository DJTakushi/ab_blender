using RmqConnection;

public static class AppInfo
{
    public const string _appVersion = "1.0.0";
}

public class PlcBlender : BackgroundService
{
    private readonly ITagAttributeFactory _tagFactory;
    private readonly IPlcFinder _plcFinder;
    private readonly Dictionary<string, PlcManager> _plc_managers = []; // TODO : make interfaces

    public PlcBlender(ITagAttributeFactory tagFactory, IPlcFinder plcFinder)
    {
        _tagFactory = tagFactory ?? throw new ArgumentNullException(nameof(tagFactory));
        _plcFinder = plcFinder ?? throw new ArgumentNullException(nameof(plcFinder));

        string? plc_address_ev = EnvVarHelper.GetPlcAddress();
        if (!string.IsNullOrEmpty(plc_address_ev))
        {
            _plc_managers.Add(plc_address_ev, new PlcManager(_tagFactory, plc_address_ev, EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol()));
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
                _plc_managers.Add(plc_ip, new PlcManager(_tagFactory, plc_ip, EnvVarHelper.GetPlcType(), EnvVarHelper.GetPlcProtocol()));
            }
        }
        foreach (KeyValuePair<string, PlcManager> entry in _plc_managers)
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
                foreach (KeyValuePair<string, PlcManager> entry in _plc_managers)
                {
                    entry.Value.readAllTags();
                }
                await Task.Delay(EnvVarHelper.GetReadTagsPeriodMs(), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteAsync: {ex.Message}");
            }
        }
    }
    public Queue<string> GetTelemetryMessages()
    {
        Queue<string> outputs = [];
        foreach (KeyValuePair<string, PlcManager> entry in _plc_managers)
        {
            outputs.Enqueue(entry.Value.genTagTelemetry());
        }
        return outputs;
    }
}
