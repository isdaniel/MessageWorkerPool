// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client.Events;

System.Console.WriteLine("Integration Testing Start");
InitialTestingTable();
using (var messageClient = new MessageClient<BalanceModel>(
        new MessageClientOptions
        {
            QueueName = GetEnvironmentVariable("QUEUENAME"),
            UserName = GetEnvironmentVariable("RABBITMQ_USERNAME", "guest"),
            Password = GetEnvironmentVariable("RABBITMQ_PASSWORD", "guest"),
            HostName = GetEnvironmentVariable("RABBITMQ_HOSTNAME"),
            Port = GetEnvironmentVariableAsUShort("RABBITMQ_PORT", 5672),
            ExchangeName = GetEnvironmentVariable("EXCHANGENAME"),
        }, NullLogger.Instance))
{
    messageClient.InitialQueue();
    int totalMessageCount = GetEnvironmentVariableAsUShort("TOTAL_MESSAGE_COUNT", 10000);
    for (int i = 1; i <= totalMessageCount; i++)
    {
        Random rnd = new Random();
        var model = new BalanceModel()
        {
            Balance = rnd.Next(1,10000),
            UserName = Guid.NewGuid().ToString("N")
        };

        string replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";
        // if(i == totalMessageCount){
        //     replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";
        //     Console.WriteLine($"ReplyQueueName:{replyQueueName}");
        // }

        messageClient.SendMessage("*", model, $"{Environment.MachineName}_{Guid.NewGuid().ToString("N")}",new Dictionary<string, object>() {
            { "targetCount",totalMessageCount}
        },replyQueueName);

        await InsertUserBalanceAsync(model);
    }

}

void InitialTestingTable()
{
    string sql = @"DROP TABLE IF EXISTS dbo.Expect;

CREATE TABLE dbo.Expect(
	UserName VARCHAR(32) primary key,
	Balance INT NOT NULL DEFAULT 0
);

DROP TABLE IF EXISTS dbo.Act;

CREATE TABLE dbo.Act(
	UserName VARCHAR(32) primary key,
	Balance INT NOT NULL DEFAULT 0
);";
    using (var conn = new SqlConnection(GetEnvironmentVariable("DBConnection")))
    {
        conn.Open();
        conn.Execute(sql);
    }
}

async Task<int> InsertUserBalanceAsync(BalanceModel model) {
    if (model == null)
    {
        throw new NullReferenceException(nameof(model));
    }

    using (var conn = new SqlConnection(GetEnvironmentVariable("DBConnection")))
    {
        await conn.OpenAsync();
        return await conn.ExecuteAsync("INSERT INTO dbo.Expect (UserName,Balance) VALUES (@UserName, @Balance)",new {
            model.UserName,
            model.Balance
        });
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

Console.ReadLine();
