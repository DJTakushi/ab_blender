namespace RmqConnection
{
    public interface IRabbitMQConnectionManager
    {
        public abstract bool IsConfigurable();
        public abstract bool IsOutputOpen();
        public abstract Task SetupConnectionsAsync();
        public abstract void PublishOutputToRabbitMQ(string message);
    }
}