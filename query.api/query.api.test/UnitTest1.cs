using Microsoft.Extensions.Options;
using query.client;
using query.service;
using Xunit.Abstractions;

namespace query.api.test
{
    public class QueryTests
    {
        private readonly ITestOutputHelper output;

        public QueryTests(ITestOutputHelper testOutputHelper)
        {
            output = testOutputHelper;
        }

        [Fact]
        public async void Test1()
        {
            if (OperatingSystem.IsWindows())
            {
                //Uri uri = new Uri("http://ubdock05:32015");
                Uri uri = new Uri("http://localhost:32015");
                var httpClient = new HttpClient() { BaseAddress = uri };

                QueryClient client = new QueryClient(httpClient, "test", "orders");

                (bool success, string result) = await client.Find(@"{ ordernumber: { $regex: '12.*' } }");

                Assert.True(success);
                Assert.DoesNotContain("ordernumber", result);

                if (success && string.IsNullOrEmpty(result))
                {
                    (success, _) = await client.Register(@"{ ordernumber: ""1234"", dealernumber: ""1234"", labcode: ""50"", articlenumber: ""343"", articlecount: ""10"" }");

                    Assert.True(success);

                    (success, result) = await client.Find(@"{ ordernumber: { $regex: '12.*' } }");

                    Assert.True(success);
                    Assert.Contains("ordernumber", result);

                    (success, result) = await client.FindAndProject(@"{ ordernumber: { $regex: '12.*' } }",
                            @"{ _id: 0, ordernumber: 1, dealernumber: 1, articlecount : 1, articlenumber: 1, registeredtime: { $dateToString: { date: ""$registeredtime"" } } }");

                    Assert.True(success);
                    Assert.Contains("ordernumber", result);

                    (success, _) = await client.Delete(@"{ ordernumber: ""1234"", dealernumber: ""1234"", labcode: ""50"" }");

                    Assert.True(success);
                }
            }
        }

        [Fact]
        public void TestInvalidCollectionAndDatabase()
        {
            var options = Options.Create(new MongoDBConnectionDetail { 
                ConnectionString = "mongodb://test" 
            });

            QueryMongoService svc = new(options);

            Assert.Throws<AggregateException>(() => svc.Query("", "orders", @"{ sourcereference: { $regex: '50_5.*' } }").Result);
            Assert.Throws<AggregateException>(() => svc.Query("test", "", @"{ sourcereference: { $regex: '50_5.*' } }").Result);
        }

        [Fact]
        public void TestInvalidQuery()
        {
            if (OperatingSystem.IsWindows())
            {
                var options = Options.Create(new MongoDBConnectionDetail
                {
                    ConnectionString = "mongodb://w2k16cache01:27017,w2k16cache02:27017,w2k16cache03:27017?replicaSet=rs0"
                });

                QueryMongoService svc = new(options);

                Assert.Throws<AggregateException>(() => svc.Query("test", "orders", @"{ sourcereference: { $rgex: '50_5.*' } }").Result);
            }
        }
    }
}
