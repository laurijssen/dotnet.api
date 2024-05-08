using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace rabbitmq.api
{
    public class RabbitMQConnectionDetail
    {
        public string? HostName { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public int Port { get; set; }

        public string? VirtualHost { get; set; }

        public string? Version { get; set; }
    }

    public interface IRabbitMQConnectionFactory
    {
        IConnection CreateConnection();
    }

    public class RabbitMQConnection : IRabbitMQConnectionFactory
    {
        private readonly RabbitMQConnectionDetail details;

        public RabbitMQConnection(IOptions<RabbitMQConnectionDetail> connectionDetails)
        {
            details = connectionDetails.Value;
        }

        public IConnection CreateConnection()
        {
            var factory = new ConnectionFactory
            {
                HostName = details.HostName,
                UserName = details.UserName,
                Password = details.Password
            };
            return factory.CreateConnection();
        }
    }
}
