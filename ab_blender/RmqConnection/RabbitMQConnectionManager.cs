using System.Threading.Tasks;
using RabbitMQ.Client;

namespace RmqConnection
{
    public class RabbitMQConnectionManager : IRabbitMQConnectionManager
    {
        private readonly ConnectionFactory _inputFactory;
        private readonly ConnectionFactory _outputFactory;

        public RabbitMQConnectionManager()
        {
            _inputFactory = CreateFactory("INPUT");
            _outputFactory = CreateFactory("OUTPUT");
        }

        private ConnectionFactory CreateFactory(string prefix)
        {
            var host = Environment.GetEnvironmentVariable($"{prefix}_RABBITMQ_HOST");
            var user = Environment.GetEnvironmentVariable($"{prefix}_RABBITMQ_USER");
            var pass = Environment.GetEnvironmentVariable($"{prefix}_RABBITMQ_PASS");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                Console.WriteLine($"Error: Missing required environment variables for {prefix}_RABBITMQ (HOST, USER, or PASS)");
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

        public async Task<IConnection> CreateInputConnection() => await _inputFactory.CreateConnectionAsync();
        public async Task<IConnection> CreateOutputConnection() => await _outputFactory.CreateConnectionAsync();
        public string InputHost => _inputFactory.HostName;
        public string OutputHost => _outputFactory.HostName;
    }
}