using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Web;

var pwhash = "gq";

byte[] key = "abcdabc"u8.ToArray();

byte[] iv = "abcdabcd"u8.ToArray();

var encToken = new byte[] { 17, 115, 0, 77, 165, 85, 41, 252 };

var encAdmin = new byte[] { 125, 51, 235, 71, 51, 85, 79, 122 };

var token = DecryptFromBytes(encToken, key, iv);
var admin = DecryptFromBytes(encAdmin, key, iv);

string user = token.Split(':')[0];
string pw = token.Split(':')[1];

var rabbit_host = "somehost";

ExchangeConfig[] exchangeConfigs =
[
    new ExchangeConfig
    {
        ExchangeName = "exchange1",
        Factories = [
            new() { VirtualHost = "/", HostName = rabbit_host, Port = 32002, UserName = user, Password = pw },
            new() { VirtualHost = "test", HostName = rabbit_host, Port = 32002, UserName = user, Password = pw }
        ],
        Queues = [
            new() { Name = "queue1" },
            new() { Name = "...." },
        ]
    },
    new ExchangeConfig
    {
        ExchangeName = "exchange2",
        Factories = [
            new() { VirtualHost = "/", HostName = rabbit_host, Port = 32002, UserName = "user", Password = pw },
            new() { VirtualHost = "test", HostName = rabbit_host, Port = 32002, UserName = "user", Password = pw }
        ],
        Queues = [
            new() { Name = "queue1" },
        ]
    }
];

Dictionary<string, bool> orders = new Dictionary<string, bool>();

Console.WriteLine(" Press [enter] to continue.");
Console.ReadLine();

await CreateAll();

async Task CreateAll()
{
    try
    {
        await CreateVhosts(token);
        await CreateUser(token);
        await CreateFromConfig(token);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"global exception occurred {ex}");
    }
}

async Task CreateVhosts(string token)
{
    HttpClient client = new() { BaseAddress = new Uri(rabbit_host) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)));

    foreach (var config in exchangeConfigs)
    {
        foreach (var factory in config.Factories.Where(f => f.VirtualHost != "/"))
        {
            var resp = await client.GetAsync($"/api/vhosts/{factory.VirtualHost}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var vh = new VirtualHost { description = factory.VirtualHost, tags = factory.VirtualHost };
                var payload = JsonSerializer.Serialize(vh);

                resp = await client.PutAsync($"/api/vhosts/{factory.VirtualHost}", new StringContent(payload, Encoding.UTF8, "application/json"));

                if (!resp.IsSuccessStatusCode)
                {
                    await Console.Out.WriteLineAsync($"creation of vhost {factory.VirtualHost} failed {resp.ReasonPhrase}");
                }
            }
            else
            {
                await Console.Out.WriteLineAsync($"vhost {factory.VirtualHost} already exists");
            }
        }
    }
}

async Task CreateUser(string token)
{
    HttpClient client = new() { BaseAddress = new Uri(rabbit_host) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)));

    var user = "PRTG";

    var resp = await client.GetAsync($"/api/users/{user}");

    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        var u = new User
        {
            password = pwhash,
            tags = "administrator"
        };

        var payload = JsonSerializer.Serialize(u);

        resp = await client.PutAsync($"/api/users/{user}", new StringContent(payload, Encoding.UTF8, "application/json"));

        if (resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"success creating user {user} {resp.ReasonPhrase}");

            var perm = new Permissions();

            payload = JsonSerializer.Serialize(perm);

            await client.PutAsync($"/api/permissions/%2F/{user}", new StringContent(payload, Encoding.UTF8, "application/json"));
        }
        else
            Console.WriteLine($"error creating user {user} {resp.ReasonPhrase}");
    }
    else
    {
        Console.WriteLine($"user {user} already exists");
    }
}

async Task CreateFromConfig(string token)
{
    HttpClient client = new() { BaseAddress = new Uri(rabbit_host) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)));

    string api;
    HttpResponseMessage resp;

    foreach (var config in exchangeConfigs)
    {
        foreach (var queueConfig in config.Queues)
        {
            var args = new Dictionary<string, object> {
                {  "x-queue-type", "quorum" },
            };

            foreach (var factory in config.Factories)
            {
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // first dead letter queues
                var queueName = $"{config.ExchangeName}-{queueConfig.Name}-dlq";

                var vhost = HttpUtility.UrlEncode(factory.VirtualHost);

                api = $"/api/queues/{vhost}/{queueName}";

                resp = await client.GetAsync(api);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {                   
                    channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
                }

                queueName = $"{config.ExchangeName}-{queueConfig.Name}";

                api = $"/api/queues/{vhost}/{queueName}";

                resp = await client.GetAsync(api);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    args = new Dictionary<string, object> {
                    { "x-dead-letter-exchange", config.ExchangeName + "-dlx" },
                    { "x-dead-letter-routing-key", $"dlx-{config.ExchangeName}-{queueConfig.Name}" },
                    {  "x-queue-type", "quorum" },
                    // { "x-message-ttl", 3600000 }
                };

                    queueName = $"{config.ExchangeName}-{queueConfig.Name}";
                    channel.QueueDeclare(queue: queueName,
                                            durable: true,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: args);

                    Console.WriteLine($"created queue {queueName} in vhost {HttpUtility.UrlDecode(vhost)}");
                }
            }
        }

        foreach (var factory in config.Factories)
        {
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var vhost = HttpUtility.UrlEncode(factory.VirtualHost);

            api = $"/api/exchanges/{vhost}/{config.ExchangeName}";

            resp = await client.GetAsync(api);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                channel.ExchangeDeclare(config.ExchangeName, "topic", true);
            }

            api = $"/api/exchanges/{vhost}/{config.ExchangeName}-dlx";

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                channel.ExchangeDeclare(config.ExchangeName + "-dlx", "topic", true, false);
            }
        }
    }

    foreach (var config in exchangeConfigs)
    {
        foreach (var queueConfig in config.Queues)
        {
            foreach (var factory in config.Factories)
            {
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // first dead letter queues
                string queueName = $"{config.ExchangeName}-{queueConfig.Name}-dlq";

                channel.QueueBind(queueName, config.ExchangeName + "-dlx", $"dlx-{config.ExchangeName}-{queueConfig.Name}");

                queueName = $"{config.ExchangeName}-{queueConfig.Name}";
                channel.QueueBind(queueName, config.ExchangeName, $"{config.ExchangeName}-{queueConfig.Name}");
            }
        }
    }
}

/*
static byte[] EncryptStringToBytes(string plainText, byte[] Key, byte[] IV) {
    byte[] encrypted;

    using (Aes rijAlg = Aes.Create()) {
        rijAlg.Key = Key;
        rijAlg.IV = IV;

        var encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

        using (var msEncrypt = new MemoryStream()) {
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)) {
                using (var swEncrypt = new StreamWriter(csEncrypt)) {
                    swEncrypt.Write(plainText);
                }
                encrypted = msEncrypt.ToArray();
            }
        }
    }
    return encrypted;
}
*/

static string DecryptFromBytes(byte[] cipherText, byte[] Key, byte[] IV)
{
    string plaintext = "";

    using (var aes = Aes.Create())
    {
        aes.Key = Key;
        aes.IV = IV;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

        using (var msDecrypt = new MemoryStream(cipherText))
        {
            using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            {
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    plaintext = srDecrypt.ReadToEnd();
                }
            }
        }
    }

    return plaintext;
}

Console.WriteLine("Press [enter] to exit.");

class Value
{
    [JsonPropertyName("src-protocol")]
    public string protocol { get; set; } = "";

    [JsonPropertyName("src-uri")]
    public string srcuri { get; set; } = "";

    [JsonPropertyName("src-queue")]
    public string srcqueue { get; set; } = "";

    [JsonPropertyName("dest-uri")]
    public string desturi { get; set; } = "";

    [JsonPropertyName("dest-queue")]
    public string destqueue { get; set; } = "";
}

class Imposix
{
    public Value value { get; set; } = new Value
    {
        protocol = "amqp091",
        srcqueue = "srcqueue",
        destqueue = "dstqueue"
    };
}

class OrderPool
{
    public Value value { get; set; } = new Value
    {
        protocol = "amqp091",
        srcqueue = "srcqueue",
        destqueue = "dstqueue"
    };
}

class Permissions
{
    public string configure { get; set; } = ".*";

    public string write { get; set; } = ".*";

    public string read { get; set; } = ".*";
}

class User
{
    public string password { get; set; } = null!;

    public string tags { get; set; } = null!;
}

class VirtualHost
{
    public string description { get; set; } = null!;

    public string tags { get; set; } = null!;

    public string default_queue_type { get; set; } = "quorum";
}

class RpcClient : IDisposable
{
    private readonly IModel channel;
    private readonly IConnection connection;
    private readonly string replyQueueName;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper = new();

    public RpcClient(ConnectionFactory factory)
    {
        connection = factory.CreateConnection();
        channel = connection.CreateModel();

        replyQueueName = channel.QueueDeclare().QueueName;

        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))
                return;

            var body = ea.Body.ToArray();
            var response = Encoding.UTF8.GetString(body);
            tcs.TrySetResult(response);
        };

        channel.BasicConsume(replyQueueName, true, consumer);
    }

    public void Dispose()
    {
        connection.Close();
    }
}

class RpcServer
{
    public RpcServer(ConnectionFactory factory)
    {
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            string response = string.Empty;

            var body = ea.Body.ToArray();
            var props = ea.BasicProperties;
            var replyProps = channel.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;

            try
            {
                var message = Encoding.UTF8.GetString(body);
                int n = int.Parse(message);
                Console.WriteLine($" [.] Fib({message})");
                response = Fib(n).ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($" [.] {e.Message}");
                response = string.Empty;
            }
            finally
            {
                var responseBytes = Encoding.UTF8.GetBytes(response);
                channel.BasicPublish(exchange: "",
                                     routingKey: props.ReplyTo,
                                     basicProperties: replyProps,
                                     body: responseBytes);
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
        };
        channel.BasicConsume(queue: "queue1",
                             autoAck: false,
                             consumer: consumer);

        Console.WriteLine(" [x] Awaiting RPC requests");
    }

    static int Fib(int n)
    {
        if (n is 0 or 1)
        {
            return n;
        }

        return Fib(n - 1) + Fib(n - 2);
    }
}
