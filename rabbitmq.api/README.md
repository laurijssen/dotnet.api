# rabbitmq

## Installing rabbitmq on kubernetes cluster

Step 1: Install the rabbitmq cluster operator

kubectl apply -f "https://github.com/rabbitmq/cluster-operator/releases/latest/download/cluster-operator.yml"

The resources will run inside the namespace rabbitmq-system. A deployment / replicaset with one desired replica. So therefore one pod running.

```
kubectl get all -n rabbitmq-system -o wide
```

```
NAME                                             READY   STATUS    RESTARTS   AGE    IP           NODE      NOMINATED NODE   READINESS GATES
pod/rabbitmq-cluster-operator-64b8d95d75-4rw8b   1/1     Running   0          7m5s   10.244.1.2   worker3   <none>           <none>
```
```
NAME                                        READY   UP-TO-DATE   AVAILABLE   AGE    CONTAINERS   IMAGES                                     SELECTOR
deployment.apps/rabbitmq-cluster-operator   1/1     1            1           7m5s   operator     rabbitmqoperator/cluster-operator:1.13.1   app.kubernetes.io/name=rabbitmq-cluster-operator
```
```
NAME                                                   DESIRED   CURRENT   READY   AGE    CONTAINERS   IMAGES                                     SELECTOR
replicaset.apps/rabbitmq-cluster-operator-64b8d95d75   1         1         1       7m5s   operator     rabbitmqoperator/cluster-operator:1.13.1   app.kubernetes.io/name=rabbitmq-cluster-operator,pod-template-hash=64b8d95d75
```

## install a rabbitmq cluster

The rabbitmq cluster operator provides a RabbitmqCluster kind with a lot of options.
Apart from that it needs a PersistentVolume to claim for every node that is part of the cluster.

So first add "x" times  PersistentVolume kind as hostpath with path /var/lib/rabbitmq/mnesia since that is where default rabbitmq stores its queues and messages.

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: rmqhostpath-1
  labels:
    type: local
spec:
  storageClassName: hostpath
  capacity:
    storage: 20Gi
  accessModes:
    - ReadWriteOnce
  hostPath:
    path: "/var/lib/rabbitmq/mnesia"

---

apiVersion: v1
kind: PersistentVolume
metadata:
  name: rmqhostpath-2
  labels:
    type: local
spec:
  storageClassName: hostpath
  capacity:
    storage: 20Gi
  accessModes:
    - ReadWriteOnce
  hostPath:
    path: "/var/lib/rabbitmq/mnesia"
```

Then add the cluster type RabbitmqCluster which was installed with the cluster operator. There need to be as many PersistentVolumes as there are replicas.
Type is NodePort here, so the cluster opens from outside and generates a port for rabbitmq's standard ports.

```rabbitmq         NodePort    10.99.22.55    <none>        5672:31075/TCP,15672:30387/TCP,15692:31299/TCP   82m```

For example, here port 31299 maps to rabbitmq's management interface port 15692.
It's not clear how to set this port manually. It should be possible though.

```yaml
apiVersion: rabbitmq.com/v1beta1
kind: RabbitmqCluster
metadata:
  labels:
    app: rabbitmq
  name: fipsrabbitmq
spec:
  replicas: 2
  image: rabbitmq
  override:
    service:
      spec:
        type: NodePort
        ports:
          - protocol: TCP
            port: 15672
            targetPort: 15672
            nodePort: 32011
          - protocol: TCP
            port: 5672
            targetPort: 5672
            nodePort: 32012
    statefulSet:
      spec:
        template:
          spec:
            containers:
              - name: rabbitmq
                ports:
                  - containerPort: 32011
                    protocol: TCP
                  - containerPort: 32012
                    protocol: TCP
  persistence:
    storageClassName: hostpath
    storage: 20
  resources:
    requests:
      cpu: 300m
      memory: 200M
    limits:
      cpu: 1000m
      memory: 500M
  rabbitmq:
    additionalPlugins:
      - rabbitmq_management
      - rabbitmq_peer_discovery_k8s
    additionalConfig: |
      cluster_formation.peer_discovery_backend = rabbit_peer_discovery_k8s
      cluster_formation.k8s.host = kubernetes.default.svc.cluster.local
      cluster_formation.k8s.address_type = hostname
      vm_memory_high_watermark_paging_ratio = 0.85
      cluster_formation.node_cleanup.interval = 10
      cluster_partition_handling = autoheal
      queue_master_locator = min-masters
      loopback_users.guest = false
      default_user = guest
      default_pass = guest
    advancedConfig: ""
```

```
Normal   Pulled        7m16s  kubelet            Container image "docker.io/istio/proxyv2:1.14.0" already present on machine
  Normal   Created       7m16s  kubelet            Created container istio-proxy
  Normal   Started       7m16s  kubelet            Started container istio-proxy
  Warning  NodeNotReady  4m12s  node-controller    Node is not ready
```

## Helm

Helm automates the whole process

Download and unzip the helm charts here:

https://charts.bitnami.com/bitnami/rabbitmq-12.0.8.tgz

run ```kubectl apply -f db-pv.yaml```

and the PersistentVolumes are deployed

Then run ```helm install rabbitmq .``` and rabbitmq runs over the 3 nodes.

After deletion (helm uninstall) the claimRef of the PersistentVolumeClaims is not removed automatically, the status will be Released.

To make the PersistentVolumes available again for the next *helm install* path the pv's.

```
kubectl patch pv rabbitmq-pv-1 -p '{"spec":{"claimRef": null}}'
kubectl patch pv rabbitmq-pv-2 -p '{"spec":{"claimRef": null}}'
kubectl patch pv rabbitmq-pv-3 -p '{"spec":{"claimRef": null}}'
```

Now ```helm install rabbitmq .``` and the persistent volumes are claimed again and rabbitmq cluster starts up.

## create consumer / publisher .NET core

For a basic message send / receive (producer / consumer) in .NET core. 

Step 1. Create a .NET core solution.

Step 2. Open a powershell prompt in that folder and add the send and recive project directories.

```
dotnet new console --name Send
mv Send/Program.cs Send/Send.cs
dotnet new console --name Receive
mv Receive/Program.cs Receive/Receive.cs
```

Add these to your solution.

Step 3. Add the nuget packages

```
cd Send
dotnet add package RabbitMQ.Client
dotnet restore
cd ../Receive
dotnet add package RabbitMQ.Client
dotnet restore
```

Step 4. Add consumer code

```C#
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

class Receive
{
    public static void Main()
    {
        var factory = new ConnectionFactory() { HostName = "192.168.56.3", Port = 31075 };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: "hello",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine(" [x] Received {0}", message);
            };
            channel.BasicConsume(queue: "hello",
                                 autoAck: true,
                                 consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}
```

Step 5. Add producer code

```C#

using RabbitMQ.Client;
using System.Text;

class Send
{
    public static void Main()
    {
        var factory = new ConnectionFactory() { HostName = "192.168.56.3", Port = 31075 };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var r = new Random(100);

            for (int i = 0; i < 10000; i++)
            {                
                channel.QueueDeclare(queue: "hello",
                                                durable: false,
                                                exclusive: false,
                                                autoDelete: false,
                                                arguments: null);

                string message = "Hello " + r.Next(1024);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                                     routingKey: "hello",
                                     basicProperties: null,
                                     body: body);
                Console.WriteLine(" [x] Sent {0}", message);
            }
        }
    }
}

```

Step 6. Build and run.

Add user

* rabbitmqctl add_user YOUR_USERNAME YOUR_PASSWORD
* rabbitmqctl set_user_tags YOUR_USERNAME administrator
* rabbitmqctl set_permissions -p / YOUR_USERNAME ".*" ".*" ".*"

* kubectl patch pvc pvc_name -p '{"metadata":{"finalizers":null}}'
* kubectl patch pv pv_name -p '{"metadata":{"finalizers":null}}'
* kubectl patch pod pod_name -p '{"metadata":{"finalizers":null}}'
