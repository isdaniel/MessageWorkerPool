// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka.Admin;
using Confluent.Kafka;
using Dapper;
using Npgsql;
using RabbitMQ.Client;

Console.WriteLine("Prepare integration scheme and queue.");
InitialTestingTable();

var broker = Environment.GetEnvironmentVariable("BROKER_TYPE") ?? "None";
var queueList = new[]{
        GetEnvironmentVariable("BALANCEWORKER_QUEUE"),
        GetEnvironmentVariable("FIBONACCI_QUEUE"),
        GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE")
    };

switch (broker.ToLower())
{
    case "rabbitmq":
        RabbitMqInit(queueList);
        break;
    case "kafka":
        await KafkaInit(queueList.Select(x=> new TopicSpecification() {
            Name = x,
            NumPartitions = 4
        })).ConfigureAwait(false);
        break;
    default:
        Console.WriteLine("No message broker specified or recognized");
        break;
}

Console.WriteLine("Already prepared integration scheme and queue!");

void InitialTestingTable()
{
    string sql = @"
DROP TABLE IF EXISTS public.act;

CREATE TABLE public.act (
    username VARCHAR(32) PRIMARY KEY,
    balance INT NOT NULL DEFAULT 0
);";
    using (var conn = new NpgsqlConnection(GetEnvironmentVariable("DBConnection")))
    {
        conn.Open();
        conn.Execute(sql);
    }
}

string GetEnvironmentVariable(string key, string defaultValue = null)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrEmpty(value) && defaultValue == null)
    {
        throw new InvalidOperationException($"Environment variable '{key}' is not set.");
    }
    return value ?? defaultValue;
}

ushort GetEnvironmentVariableAsUShort(string key, ushort defaultValue)
{
    var value = Environment.GetEnvironmentVariable(key);
    return ushort.TryParse(value, out ushort parsedValue) ? parsedValue : defaultValue;
}

void RabbitMqInit(string[] queueList)
{
    var userName = GetEnvironmentVariable("RABBITMQ_USERNAME", "guest");
    var password = GetEnvironmentVariable("RABBITMQ_PASSWORD", "guest");
    var hostName = GetEnvironmentVariable("RABBITMQ_HOSTNAME");
    var port = GetEnvironmentVariableAsUShort("RABBITMQ_PORT", 5672);
    var exchangeName = GetEnvironmentVariable("EXCHANGENAME");

    var connfac = new ConnectionFactory()
    {
        Uri = new Uri($"amqp://{userName}:{password}@{hostName}:{port}")
    };

    using (var connection = connfac.CreateConnection())
    using (var channel = connection.CreateModel())
    {
        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null);
        foreach (var queueName in queueList)
        {
            channel.QueueDeclare(queueName, true, false, false, null);
            channel.QueueBind(queueName, exchangeName, queueName);
        }
    }

}

async Task KafkaInit(IEnumerable<TopicSpecification> topics)
{
   using var adminClient = new AdminClientBuilder(config: new AdminClientConfig { BootstrapServers = EnvironmentVAR.HOSTNAME, Acks = Acks.All }).Build();

    try
    {
        await adminClient.CreateTopicsAsync(topics).ConfigureAwait(false);
    }
    catch (CreateTopicsException ex) when (
        ex.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
    {
        //ignore
    }
}

public static class EnvironmentVAR
{
    public static readonly string HOSTNAME = Environment.GetEnvironmentVariable("KAFKA_HOSTNAME") ?? "localhost:9092";
    public static readonly string GROUPID = Environment.GetEnvironmentVariable("KAFKA_GROUPID") ?? "integrationGroup";
}
