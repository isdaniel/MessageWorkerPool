// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using RabbitMQ.Client;

namespace IntegrationTester
{
    public class FibonacciWorkerTests
    {
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

            TestHelpers.SendingMessage(JsonSerializer.Serialize(new FibonacciModel()
            {
                Value = input
            }), correlationId, queueName, replyQueue);


            (int act, IModel channel, IConnection connection) = await TestHelpers.WaitForMessageResult(replyQueue, (message) => int.Parse(message));

            //assert
            Action beforeDelFunc = () => channel.QueueDeclarePassive(replyQueue);
            beforeDelFunc.Should().NotThrow();
            channel.QueueDelete(replyQueue).Should().Be(0);
            act.Should().Be(expect);

            channel.Close();
            connection.Close();
        }
    }
}

