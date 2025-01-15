// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

Console.WriteLine("Prepare integration scheme and queue.");
InitialTestingTable();

var userName = GetEnvironmentVariable("RABBITMQ_USERNAME", "guest");
var password = GetEnvironmentVariable("RABBITMQ_PASSWORD", "guest");
var hostName = GetEnvironmentVariable("RABBITMQ_HOSTNAME");
var port = GetEnvironmentVariableAsUShort("RABBITMQ_PORT", 5672);
var exchangeName = GetEnvironmentVariable("EXCHANGENAME");

var connfac = new ConnectionFactory()
{
    Uri = new Uri($"amqp://{userName}:{password}@{hostName}:{port}")
};

var queueList = new []{
    GetEnvironmentVariable("BALANCEWORKER_QUEUE"),
    GetEnvironmentVariable("FIBONACCI_QUEUE"),
    GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE")
};

using (var connection = connfac.CreateConnection())
using (var channel = connection.CreateModel())
{
    channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null);
    foreach (var queueName in queueList) {
        channel.QueueDeclare(queueName, true, false, false, null);
        channel.QueueBind(queueName, exchangeName, queueName);
    }

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

//todo insert verify table

//create table.
//create worker add mock data.
