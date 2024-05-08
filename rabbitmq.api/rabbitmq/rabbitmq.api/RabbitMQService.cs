using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using System.Runtime.CompilerServices;

namespace rabbitmq.api
{
    public class RabbitServer
    {
        private ConcurrentDictionary<string, ConnectionFactory> factories { get; } = new();

        private ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper = new();

        private RabbitMQConnectionDetail bluePrint;

        public RabbitServer(RabbitMQConnectionDetail detail)
        {
            bluePrint = detail;

            Console.WriteLine($"cluster [{detail.HostName} {detail.UserName} {detail.Port}]");
        }

        public IResult SendMessage(string exchange, string routingkey, string message, string vhost)
        {
            var channel = connect(vhost);

            try
            {
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: exchange, routingKey: routingkey, body: body);

                Console.WriteLine($"{DateTime.Now} {exchange} {routingkey}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Results.BadRequest(ex.Message);
            }
            return Results.Ok();
        }

        public IResult SendMessageBinary(string exchange, string routingkey, string message, string vhost)
        {
            var channel = connect(vhost);

            var buffer = new byte[((message.Length * 3) + 3) / 4 -
                (message[message.Length - 1] == '=' ? message[message.Length - 2] == '=' ? 2 : 1 : 0)];

            if (Convert.TryFromBase64String(message, buffer, out int _))
            {
                try
                {
                    channel.BasicPublish(exchange: exchange, routingKey: routingkey, body: buffer);

                    Console.WriteLine($"{DateTime.Now} {exchange} {routingkey}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return Results.BadRequest(ex.Message);
                }
            }
            else
            {
                string error = $"error sending message {routingkey} base64({message})";

                Console.WriteLine(error);
                return Results.BadRequest(error);
            }

            return Results.Ok();
        }

        public Task<string> SendMessageWithResponse(string exchange, string routingkey, string message, string vhost, int timeout, CancellationToken ct = default)
        {
            var channel = connect(vhost);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var replyQueueName = startResponseConsumer(channel);

            DelayedExecute.Do(() => {
                if (channel != default && channel.IsOpen)
                {
                    Console.WriteLine($"{DateTime.Now} channel close {replyQueueName}");
                    channel.Close();
                }
            }, timeout);

            var props = channel.CreateBasicProperties();

            props.CorrelationId = Guid.NewGuid().ToString();
            props.ReplyTo = replyQueueName;

            var body = Encoding.UTF8.GetBytes(message);

            var tcs = new TaskCompletionSource<string>();
            callbackMapper.TryAdd(props.CorrelationId, tcs);

            channel.BasicPublish(exchange: exchange, routingKey: routingkey, body: body, basicProperties: props);

            ct.Register(() => callbackMapper.TryRemove(props.CorrelationId, out _));

            Console.WriteLine($"{DateTime.Now} message: {exchange} {routingkey}");

            return tcs.Task;
        }

        private IModel connect(string vhost)
        {
            if (!factories.TryGetValue(vhost, out var factory))
            {
                factory = new ConnectionFactory
                {
                    HostName = bluePrint.HostName,
                    UserName = bluePrint.UserName,
                    Password = bluePrint.Password,
                    Port = bluePrint.Port,
                    VirtualHost = vhost
                };
                factories.TryAdd(vhost, factory); // does not matter if it doesnt get added then it will be next time
            }

            var connection = factory.CreateConnection();

            return connection.CreateModel();
        }

        private string startResponseConsumer(IModel? channel)
        {
            // maybe use this instead of timer to remove copnsumer in client
            //Dictionary<string, object> args = new() { { "x-expires", 3600000 } };

            string replyQueueName = channel.QueueDeclare().QueueName;

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
            {
                if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))
                    return;

                var body = ea.Body.ToArray();
                var response = Encoding.UTF8.GetString(body);
                tcs.TrySetResult(response);

                if (channel != default)
                {
                    channel.Close();
                    channel = default;
                }
            };

            channel.BasicConsume(replyQueueName, true, consumer);

            return replyQueueName;
        }
    }
}
