using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace query.service
{
    public class MongoDBConnectionDetail
    {
        public string? ConnectionString { get; set; }
    }

    public class MongoDBConnection
    {
        public MongoClient Client { get; set; }

        public MongoDBConnection(IOptions<MongoDBConnectionDetail> details)
        {
            Client = new (details.Value.ConnectionString);
        }
    }
}
