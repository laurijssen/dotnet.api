using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Prometheus;
using System.Reflection;
using static System.String;

namespace query.service
{
    public class QueryMongoService
    {
        public class MyDateConverter : IsoDateTimeConverter
        {
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("$date");

                base.WriteJson(writer, value, serializer);
                writer.WriteEndObject();
            }
        }

        class LowercasePropertyResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);

                if (prop != null && prop.PropertyName != null)
                {
                    prop.PropertyName = prop.PropertyName.ToLower();
                }

                return prop!;
            }
        }

        private MongoDBConnection Connection { get; set; }

        private static readonly Counter Reads = Metrics.CreateCounter("queryapi_number_of_reads", "number of queries");
        private static readonly Counter Deletes = Metrics.CreateCounter("queryapi_number_of_deletes", "number of deletes");
        private static readonly Counter Updates = Metrics.CreateCounter("queryapi_number_of_updates", "number of updates");

        public QueryMongoService(IOptions<MongoDBConnectionDetail> opt) 
        {
            Connection = new MongoDBConnection(opt);
        }

        public async Task<string> Test()
        {
            return await Task.FromResult("test");
        }

        public async Task<string> Query(string database, string collection, string qry)
        {
            try
            {
                var db = Connection.Client.GetDatabase(database);

                var col = db.GetCollection<BsonDocument>(collection);

                var filter = BsonDocument.Parse(qry);

                var projection = Builders<BsonDocument>.Projection.Exclude("_id");

                // limit to 10 should be enough
                var result = (await col.FindAsync(filter, new FindOptions<BsonDocument>
                {
                    Projection = projection,
                    Limit = 10
                })).ToList();

                string json = "";

                foreach (var r in result)
                { 
                    if (!IsNullOrEmpty(json)) 
                        json += ","; 
                    json += r.ToJson();                    
                }

                if (!IsNullOrEmpty(json))
                    json = "[" + json + "]";
                
                return json;
            }
            catch (MongoCommandException ex)
            {              
                throw new ArgumentException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
            finally
            {
                Reads.Inc();
            }
        }

        public string QueryAndProject(string database, string collection, string qry, string project)
        {
            try
            {
                var db = Connection.Client.GetDatabase(database);

                var col = db.GetCollection<BsonDocument>(collection);

                var filter = BsonDocument.Parse(qry);

                var result = col.Find(filter).Project(project).ToList();

                Console.WriteLine($"{DateTime.Now} found orders");

                string json = "";

                foreach (var r in result)
                {
                    if (!IsNullOrEmpty(json))
                        json += ",";
                    json += r.ToJson();
                }

                if (!IsNullOrEmpty(json))
                    json = "[" + json + "]";

                return json;
            }
            catch (MongoCommandException ex)
            {
                throw new ArgumentException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
            finally
            {
                Reads.Inc();
            }
        }

        public void Register(string database, string collection, string document)
        {
            try
            {
                var db = Connection.Client.GetDatabase(database);

                var col = db.GetCollection<BsonDocument>(collection);

                var options = new JsonSerializerSettings
                {
                    ContractResolver = new LowercasePropertyResolver(),
                    Converters = new List<JsonConverter> { new MyDateConverter() }
                };

                var bson = BsonDocument.Parse(document);

                col.InsertOne(bson);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
            finally
            {
                Updates.Inc();
            }
        }

        public void Delete(string database, string collection, string filter)
        {
            try
            {
                var db = Connection.Client.GetDatabase(database);

                var col = db.GetCollection<BsonDocument>(collection);

                var bson = BsonDocument.Parse(filter);

                col.DeleteMany(bson);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
            finally
            {
                Deletes.Inc();
            }
        }

        private string toJson(BsonDocument bson)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BsonDataWriter(stream))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(writer, bson);

                    return writer.ToString()!;
                }
            }
        }
    }
}
