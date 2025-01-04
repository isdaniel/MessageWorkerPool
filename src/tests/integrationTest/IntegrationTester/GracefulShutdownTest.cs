// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
public class GracefulShutdownTest
{
    private const int DefaultMessageCount = 10000;

    [Fact]
    public async Task WorkerConsumeMessage_BalanceComparisonTest()
    {
        // Arrange
        var totalMessageCount = GetEnvironmentVariableAsInt("TOTAL_MESSAGE_COUNT", DefaultMessageCount);
        var rabbitMqSetting = new RabbitMqSetting
        {
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort port) ? port : (ushort)5672,
        };
        var replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";
        (ResponseMessage act, IModel channel, IConnection connection) = await WaitForMessageResult(replyQueueName, (message) => JsonSerializer.Deserialize<ResponseMessage>(message));

        var expectedList = (await GetAllBalanceFrom("dbo.Expect")).ToList();
        var actualList = (await GetAllBalanceFrom("dbo.Act")).ToList();

        // Assert
        expectedList.Count.Should().Be(totalMessageCount);
        ValidateBalanceComparison(actualList, expectedList);
        act.Status.Should().Be("OK!");
    }

    [Theory]
    [InlineData(-20, -6765)]
    [InlineData(-19, 4181)]
    [InlineData(-18, -2584)]
    [InlineData(-17, 1597)]
    [InlineData(-16, -987)]
    [InlineData(-15, 610)]
    [InlineData(-14, -377)]
    [InlineData(-13, 233)]
    [InlineData(-12, -144)]
    [InlineData(-11, 89)]
    [InlineData(-10, -55)]
    [InlineData(-9, 34)]
    [InlineData(-8, -21)]
    [InlineData(-7, 13)]
    [InlineData(-6, -8)]
    [InlineData(-5, 5)]
    [InlineData(-4, -3)]
    [InlineData(-3, 2)]
    [InlineData(-2, -1)]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 8)]
    [InlineData(7, 13)]
    [InlineData(8, 21)]
    [InlineData(9, 34)]
    [InlineData(10, 55)]
    [InlineData(11, 89)]
    [InlineData(12, 144)]
    [InlineData(13, 233)]
    [InlineData(14, 377)]
    [InlineData(15, 610)]
    [InlineData(16, 987)]
    [InlineData(17, 1597)]
    [InlineData(18, 2584)]
    [InlineData(19, 4181)]
    [InlineData(20, 6765)]
    public async Task RPC_WorkerConsumeMessage_FibonacciWorkerTest(int input, int expect)
    {
        string correlationId = Guid.NewGuid().ToString("N");
        string queueName = Environment.GetEnvironmentVariable("FIBONACCI_QUEUE") ?? "integrationTesting_fibonacciQ";
        string replyQueue = $"{queueName}_{correlationId}";

        SendingMessage(input, correlationId, queueName, replyQueue);


        (int act,IModel channel,IConnection connection) = await WaitForMessageResult(replyQueue,(message) => int.Parse(message));

        //assert
        Action beforeDelFunc = () => channel.QueueDeclarePassive(replyQueue);
        beforeDelFunc.Should().NotThrow();
        channel.QueueDelete(replyQueue).Should().Be(0);
        Action afterDelFunc = () => channel.QueueDeclarePassive(replyQueue);
        afterDelFunc.Should().Throw<OperationInterruptedException>();
        act.Should().Be(expect);

        channel.Close();
        connection.Close();
    }

    private async Task<(TResult, IModel, IConnection)> WaitForMessageResult<TResult>(string replyQueue,Func<string,TResult> action)
    {
        IConnection connection;
        IModel channel;

        var rabbitMqSetting = new RabbitMqSetting
        {
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : (ushort)5672,
        };

        var factory = new ConnectionFactory { Uri = rabbitMqSetting.GetUri() };
        TaskCompletionSource<TResult> messageReceived = new TaskCompletionSource<TResult>();
        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        channel.QueueDeclare(replyQueue, true, false, false, null);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, e) =>
        {
            try
            {
                var message = Encoding.UTF8.GetString(e.Body.Span);
                messageReceived.SetResult(action(message));
                channel.BasicAck(e.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                messageReceived.SetException(ex);
            }
        };

        channel.BasicQos(0, 1, false);
        channel.BasicConsume(replyQueue, false, consumer);

        TResult res = await messageReceived.Task;

        return (res, channel, connection);
    }

    private void SendingMessage(int input, string correlationId, string queueName, string replyQueue)
    {
        using (MessageClient messageClient = new MessageClient(new MessageClientOptions()
        {
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort port) ? port : (ushort)5672,
            QueueName = queueName,
            ExchangeName = Environment.GetEnvironmentVariable("FIBONACCI_EXCHANGE") ?? "integrationTesting_fibonacci_Exchange"
        }))
        {
            messageClient.InitialQueue();
            messageClient.PublishMessage("*", JsonSerializer.Serialize(new FibonacciModel()
            {
                Value = input
            }), correlationId, null, replyQueue);
        }

    }

    private static void ValidateBalanceComparison(List<BalanceModel> actList, List<BalanceModel> expectList)
    {
        expectList.Count.Should().Be(actList.Count);
        expectList.Should().BeEquivalentTo(actList, options => options.WithStrictOrdering());
    }

    private static async Task<IEnumerable<BalanceModel>> GetAllBalanceFrom(string tableName)
    {
        using var conn = new SqlConnection(Environment.GetEnvironmentVariable("DBConnection"));
        await conn.OpenAsync().ConfigureAwait(false);
        return await conn.QueryAsync<BalanceModel>($"SELECT UserName, Balance FROM {tableName}").ConfigureAwait(false);
    }

    private static int GetEnvironmentVariableAsInt(string key, ushort defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out int parsedValue) ? parsedValue : defaultValue;
    }

    public class BalanceModel
    {
        public string UserName { get; set; }
        public int Balance { get; set; }
    }
}

