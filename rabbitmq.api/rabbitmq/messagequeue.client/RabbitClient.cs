
using Newtonsoft.Json;
using System.Text;

using static System.String;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Net.Http;
//using System.Reflection.Metadata;

namespace rabbitmq.client
{
    public interface IRabbitClient
    {
        Task<(bool, string)> SendMessageAsync(string queue, string message);

        Task<(bool, string)> SendBinaryMessageAsync(string queue, byte[] message);

        Task<(bool, string)> CallRPCAsync(string queue, string message, int timeout=30000);
    }

    public class RabbitClient : IRabbitClient
    {
        private string _vhost;

        public HttpClient Client { get; set; }

        public string Exchange { get; set; }

        public RabbitClient(HttpClient client, string exchange, string vhost="/")
        {
            var baseAddress = client.BaseAddress.ToString();

            if (!baseAddress.EndsWith("/")) baseAddress += '/';

            client.BaseAddress = new Uri(baseAddress);

            _vhost = vhost;

            Client = client;
            Exchange = exchange;
        }

        public RabbitClient(string baseAddress, string exchange, string vhost = "/")
            : this(new HttpClient { BaseAddress = new Uri(fixBaseAddress(baseAddress)) }, exchange, vhost)
        {
        }

        public async Task<(bool, string)> SendMessageAsync(string queue, string message)
        {
            try
            {
                if (IsNullOrEmpty(queue)) return (false, "empty routingkey");
                if (IsNullOrEmpty(message)) return (false, "empty message");

                var kv = new Dictionary<string, string> {
                    { "exchange", Exchange },
                    { "routingkey", queue },
                    { "message", message },
                    { "vhost", _vhost }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("sendmessage", content);

                return (response.IsSuccessStatusCode, response.ReasonPhrase ?? "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> SendBinaryMessageAsync(string queue, byte[] message)
        {
            try
            {
                if (IsNullOrEmpty(queue)) return (false, "empty routingkey");
                if (message == null || message.Length == 0) return (false, "empty message");

                var data = Convert.ToBase64String(message);

                var kv = new Dictionary<string, string> {
                    { "exchange", Exchange },
                    { "routingkey", queue },
                    { "message", data },
                    { "vhost", _vhost }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");
                var response = await Client.PostAsync("sendbinarymessage", content);

                return (response.IsSuccessStatusCode, response.ReasonPhrase ?? "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> CallRPCAsync(string queue, string message, int timeout=30000)
        {
            try
            {
                if (IsNullOrEmpty(queue)) return (false, "empty routingkey");
                if (IsNullOrEmpty(message)) return (false, "empty message");

                var kv = new Dictionary<string, string> {
                    { "exchange", Exchange },
                    { "routingkey", queue },
                    { "message", message },
                    { "vhost", _vhost },
                    { "timeout", $"{timeout}" }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("sendmessagewithresponse", content);

                if (response.IsSuccessStatusCode)
                    return (true, await response.Content.ReadAsStringAsync());

                return (response.IsSuccessStatusCode, response.ReasonPhrase ?? "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string fixBaseAddress(string baseAddress)
        {
            if (!baseAddress.EndsWith("/")) baseAddress += '/';

            return baseAddress;
        }
    }
}
