// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit.Sdk;

public class RabbitMqSetting 
{
    /// <summary>
    /// The uri to use for the connection.
    /// </summary>
    /// <returns></returns>
    public Uri GetUri()
    {
        return new Uri($"amqp://{UserName}:{Password}@{HostName}:{Port}");
    }

    /// <summary>
    /// Rabbit Mq Port
    /// </summary>
    public ushort Port { get; set; }
    public string UserName { get; set; }
    /// <summary>
    /// Password to use when authenticating to the server.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// The host to connect to
    /// </summary>
    public string HostName { get; set; }
}

public class ResponeMessage
{
    public int ProcessCount { get; set; }
    public string Status { get; set; }
}

public class GracefulShutdownTest
{
    private const int DefaultMessageCount = 10000;
    private const string HOST = "127.0.0.1";
    private const string ConnectionString = $"Data Source={HOST};Initial Catalog=orleans;User ID=sa;Password=test.123;TrustServerCertificate=true;";

    [Fact]
    public async Task WorkerConsumeMessage_BalanceComparisonTest()
    {
        // Arrange
        var totalMessageCount = GetEnvironmentVariableAsInt("TOTAL_MESSAGE_COUNT", DefaultMessageCount);
        var rabbitMqSetting = GetRabbitMqSettings();
        var replayQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";

        var factory = new ConnectionFactory { Uri = rabbitMqSetting.GetUri() };
        var messageReceived = new TaskCompletionSource();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(replayQueueName, true, false, false, null);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, e) =>
        {
            try
            {
                var message = Encoding.UTF8.GetString(e.Body.Span);
                //var responseMessage = JsonSerializer.Deserialize<ResponeMessage>(message);
                Console.WriteLine($"IntegrationTest Finish, reply message: {message}");
                // Perform any additional checks on responseMessage if needed
                messageReceived.SetResult();
                channel.BasicAck(e.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                messageReceived.SetException(ex);
            }
        };

        channel.BasicQos(0, 1, false);
        channel.BasicConsume(replayQueueName, false, consumer);

        // Act
        await messageReceived.Task; // Wait asynchronously for the message
        var expectedList = (await GetAllBalanceFrom("dbo.Expect")).ToList();
        var actualList = (await GetAllBalanceFrom("dbo.Act")).ToList();

        // Assert
        expectedList.Count.Should().Be(totalMessageCount);
        ValidateBalanceComparison(actualList, expectedList);
    }

    private RabbitMqSetting GetRabbitMqSettings() => new RabbitMqSetting
    {
        UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
        Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
        HostName = HOST,
        Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort port) ? port : (ushort)5672,
    };

    private static void ValidateBalanceComparison(List<BalanceModel> actList, List<BalanceModel> expectList)
    {
        expectList.Count.Should().Be(actList.Count);
        expectList.Should().BeEquivalentTo(actList, options => options.WithStrictOrdering());
    }

    private static async Task<IEnumerable<BalanceModel>> GetAllBalanceFrom(string tableName)
    {
        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        return await conn.QueryAsync<BalanceModel>($"SELECT UserName, Balance FROM {tableName}").ConfigureAwait(false);
    }

    private static ushort GetEnvironmentVariableAsUShort(string key, ushort defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return ushort.TryParse(value, out ushort parsedValue) ? parsedValue : defaultValue;
    }

    private static int GetEnvironmentVariableAsInt(string key, ushort defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out int parsedValue) ? parsedValue : defaultValue;
    }

    private static string GetEnvironmentVariable(string key, string defaultValue = null)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value) && defaultValue == null)
        {
            throw new InvalidOperationException($"Environment variable '{key}' is not set.");
        }
        return value ?? defaultValue;
    }


    public class BalanceModel
    {
        public string UserName { get; set; }
        public int Balance { get; set; }
    }

}


