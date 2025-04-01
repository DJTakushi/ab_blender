using RabbitMQ.Client;

namespace RmqConnection
{
    public class RabbitMQConnectionManager : IRabbitMQConnectionManager
    {
        private ConnectionFactory? _outputFactory = null;
        private IConnection? _outputConnection = null;
        private IChannel? _outputChannel = null;

        public bool IsConfigurable()
        {
            return !string.IsNullOrEmpty(EnvVarHelper.GetRmqHost()) &&
                   !string.IsNullOrEmpty(EnvVarHelper.GetRmqUser()) &&
                   !string.IsNullOrEmpty(EnvVarHelper.GetRmqPass()) &&
                   !string.IsNullOrEmpty(EnvVarHelper.GetRmqExchange()) &&
                   !string.IsNullOrEmpty(EnvVarHelper.GetRmqRoutingKey()) &&
                   !string.IsNullOrEmpty(EnvVarHelper.GetRmqConnectionName());
        }
        public bool IsOutputOpen(){
            return _outputConnection?.IsOpen == true && _outputChannel?.IsOpen == true;
        }

        private ConnectionFactory CreateFactory(string prefix)
        {
            var host = Environment.GetEnvironmentVariable($"{prefix}RABBITMQ_HOST");
            var user = Environment.GetEnvironmentVariable($"{prefix}RABBITMQ_USER");
            var pass = Environment.GetEnvironmentVariable($"{prefix}RABBITMQ_PASS");
            var name = Environment.GetEnvironmentVariable($"{prefix}RABBITMQ_CONNECTION_NAME");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(name))
            {
                Console.WriteLine($"Error: Missing required environment variables for {prefix}RABBITMQ (HOST, USER, PASS, CONNECTION_NAME)");
                Environment.Exit(1);
            }

            return new ConnectionFactory
            {
                HostName = host,
                UserName = user,
                Password = pass,
                AutomaticRecoveryEnabled = true
            };
        }

        private async Task<IConnection> CreateConnection(ConnectionFactory factory, string name) => await factory.CreateConnectionAsync(name);

        public async Task SetupConnectionsAsync()
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

                    _outputFactory = CreateFactory(""); // TODO : use "OUTPUT_"
                    _outputConnection = await CreateConnection(_outputFactory, EnvVarHelper.GetRmqConnectionName());
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
        public async void PublishOutputToRabbitMQ(string message)
        {
            if (_outputChannel == null)
            {
                Console.WriteLine("Output channel is not open.");
            }
            else
            {
                var body = System.Text.Encoding.UTF8.GetBytes(message);
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