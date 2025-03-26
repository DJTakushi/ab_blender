using RabbitMQ.Client;

namespace RmqConnection
{
    public interface IRabbitMQConnectionManager
    {
        public ConnectionFactory CreateFactory(string prefix);
        public Task<IConnection> CreateConnection(ConnectionFactory factory, string name);
    }
}