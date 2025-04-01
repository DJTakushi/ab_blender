using RmqConnection;

class Program
{
    static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<PlcBlender>();
                services.AddSingleton<IPlcFinder, PlcFinder>();
                services.AddSingleton<IRabbitMQConnectionManager, RabbitMQConnectionManager>();
                services.AddSingleton<ITagAttributeFactory, TagAttributeFactory>();

                // services.AddLogging(logging =>
                // {
                //     logging.AddConsole();
                //     logging.SetMinimumLevel(Environment.GetEnvironmentVariable("DEBUG") != null
                //         ? LogLevel.Debug
                //         : LogLevel.Information);
                // });
            });
}