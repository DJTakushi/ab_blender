using RabbitMQ.Client;

namespace RmqConnection
{
    public interface IRabbitMQConnectionManager
    {
        public Task<IConnection> CreateInputConnection();
        public Task<IConnection> CreateOutputConnection();

        // IConnection CreateInputConnection();
        // IConnection CreateOutputConnection();
        string InputHost { get; }
        string OutputHost { get; }
    }
}