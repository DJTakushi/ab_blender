using System.Threading.Tasks;
using RabbitMQ.Client;

namespace RmqConnection
{
    public class RabbitMQConnectionManager : IRabbitMQConnectionManager
    {
        public ConnectionFactory CreateFactory(string prefix)
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

        public async Task<IConnection> CreateConnection(ConnectionFactory factory, string name) => await factory.CreateConnectionAsync(name);
    }
}