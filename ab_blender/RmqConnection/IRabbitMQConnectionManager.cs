using RabbitMQ.Client;

namespace RmqConnection
{
    public interface IRabbitMQConnectionManager
    {
        public abstract bool IsConfigurable();
        public abstract bool IsOutputOpen();
        public abstract void SetupConnectionsAsync();
        public abstract void PublishOutputToRabbitMQ(string message);
    }
}