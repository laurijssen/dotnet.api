using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ
{
    class RabbitQueue
    {
        public string Name { get; set; } = null!;
    }

    class ExchangeConfig
    {
        public string ExchangeName { get; set; } = null!;

        public ConnectionFactory[] Factories { get; set; } = null!;

        public RabbitQueue[] Queues { get; set; } = null!;
    }
}
