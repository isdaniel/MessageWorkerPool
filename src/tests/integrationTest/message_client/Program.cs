// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client.Events;

Console.WriteLine(GetEnvironmentVariable("DBConnection"));

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
    for (int i = 0; i < GetEnvironmentVariableAsUShort("TOTAL_MESSAGE_COUNT", 10000); i++)
    {
        Random rnd = new Random();
        var model = new BalanceModel()
        {
            Balance = rnd.Next(1,10000),
            UserName = Guid.NewGuid().ToString("N")
        };

        messageClient.SendMessage("*", model, $"{Environment.MachineName}_{Guid.NewGuid().ToString("N")}");

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
