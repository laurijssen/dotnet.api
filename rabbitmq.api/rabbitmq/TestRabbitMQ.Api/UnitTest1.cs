using FluentAssertions;
using Moq;
using Moq.Protected;
using rabbitmq.client;
using System.Text;

using rabbitmq.api;
using System.Net;

namespace TestRabbitMQ.Api
{
    public class UnitTest1
    {
        [Fact]
        public async void TestRealSendMessage()
        {
            if (OperatingSystem.IsLinux()) return;

            //Uri uri = new Uri("http://ubdock05.fujicolor.nl:32005");

            /*
             * taloscluster with ingress running on rabbitmqapi filter
            */
            //string baseAddr = "https://10.203.32.240:30443/rabbitmqapi/";

            string baseAddr = "http://127.0.0.1:32005";

            var handler = new HttpClientHandler()
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => { return true; }
            };

            HttpClient httpClient = new(handler) { BaseAddress = new Uri(baseAddr) };
            
            var client = new RabbitClient(httpClient, "rops", "/");

            for (int i = 0; i < 2; i++)
            {
                var loc = await client.GetSortLocation("rops-getsortlocation", new()
                {
                    LabCode = "50",
                    DealerNumber = "9020"
                });

                Console.Out.WriteLine(loc);
            }

            httpClient = new(handler) { BaseAddress = new Uri(baseAddr) };

            client = new RabbitClient(httpClient, "rops", "test");

            for (int i = 0; i < 2; i++)
            {
                var loc = await client.GetSortLocation("rops-getsortlocation", new()
                {
                    LabCode = "50",
                    DealerNumber = "9020"
                });

                Console.Out.WriteLine(loc);
            }

            (bool success, string reason) = await client.SendMessageAsync("rops-scanpriceundo", "{ message: \"test\" }");

            Console.WriteLine(reason);

            (success, reason) = await client.SendBinaryMessageAsync("rops-scanpriceundo", new byte[] { 64, 65, 66 });
            Console.WriteLine(reason);
        }

        [Fact]
        public void TestBaseAdress()
        {
            string baseAddr = "http://10.203.32.240:30080/rabbitmqapi";

            var client = new RabbitClient(baseAddr, "rops");

            client.Client.BaseAddress.Should().NotBeNull();
            client.Client.BaseAddress!.ToString().EndsWith("/").Should().Be(true);

            baseAddr = "http://10.203.32.240:30080/rabbitmqapi/";

            client = new RabbitClient(baseAddr, "rops");
        }

        [Fact]
        public async void TestSendMessageClient()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.OK);

            (bool success, string reason) = await client.SendMessageAsync("dummy", "dummy");

            success.Should().BeTrue();
            string.Compare(reason, "ok", true).Should().Be(0);
        }

        [Fact]
        public async void TestSendBinaryMessage()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.OK);

            (bool success, string reason) = await client.SendBinaryMessageAsync("dummy", new byte[1]);

            success.Should().BeTrue();
            (string.Compare(reason, "ok", true)).Should().Be(0);
        }

        [Fact]
        public async void TestSendMessageWithResponse()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.OK);

            (bool success, string res) = await client.CallRPCAsync("dummy", "dummy");

            success.Should().BeTrue();
            string.Compare(res, content, true).Should().Be(0);
        }

        [Fact]
        public async void TestSendMessageWithResponseFail()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.OK);

            (bool success, string res) = await client.CallRPCAsync("", "dummy");

            success.Should().BeFalse();
            string.Compare(res, "empty routingkey", true).Should().Be(0);

            (success, res) = await client.CallRPCAsync("dummy", "");

            success.Should().BeFalse();
            string.Compare(res, "empty message", true).Should().Be(0);

        }

        [Fact]
        public async void TestSendMessageFail()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.BadRequest);

            (bool success, string reason) = await client.SendMessageAsync("dummy", "dummy");

            success.Should().BeFalse();
            (string.Compare(reason, "bad request", true)).Should().Be(0);
        }

        [Fact]
        public async void TestSendBinaryMessageFail()
        {
            var content = @"{ 'exchange': 'rops', 'routingkey':'rops-freestorage', 'message': 'msg' }";

            var client = GetMockClient(content, System.Net.HttpStatusCode.BadRequest);

            (bool success, string reason) = await client.SendBinaryMessageAsync("dummy", Encoding.UTF8.GetBytes("dummy"));

            success.Should().BeFalse();
            (string.Compare(reason, "bad request", true)).Should().Be(0);
        }

        [Fact]
        public void TestRabbitService()
        {
            RabbitServer s = GetMockService();

            //s.SendMessage("exchange", "queue", "{}", "/");
        }

        private RabbitClient GetMockClient(string content, System.Net.HttpStatusCode code)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = code,
                   Content = new StringContent(content),
               })
               .Verifiable();

            Uri uri = new Uri("http://rabbit.api.test.com");
            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = uri };
            return new RabbitClient(httpClient, "path");
        }

        private RabbitServer GetMockService()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);

            // not sure what to do
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.NotFound,
                   Content = new StringContent(""),
               })
               .Verifiable();

            RabbitMQConnectionDetail d = new ()
            {
                HostName = "wrong",
                Port = 1,
                Password = "none",
                UserName = "guest"
            };

            return new RabbitServer(d);
        }
    }
}