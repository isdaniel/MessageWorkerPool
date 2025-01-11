// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using RabbitMQ.Client;

namespace IntegrationTester
{
    public class LongRunningBatchTaskTests
    {
        [Theory]
        [InlineData(10000, 50005000, 100)]
        [InlineData(10000, 50005000, 1234)]
        [InlineData(1000, 500500, 50)]
        [InlineData(1000, 500500, 23)]
        [InlineData(10, 55, 101)]
        [InlineData(1, 1, 1)]
        [InlineData(3, 6, 1)]
        [InlineData(100, 5050, 10)] 
        [InlineData(103, 5356, 10)]
        [InlineData(10, 55, 10)]    
        [InlineData(0, 0, 10)]     
        [InlineData(10, 55, 1)]     
        public async Task WorkerConsumeMessage_LongRunningBatchTaskTest(int total, int expect, int batch)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            string queueName = Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE") ?? "LongRunningBatchTaskQ";
            string replyQueue = Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_REPLYQUEUE");
            TestHelpers.SendingMessage(JsonSerializer.Serialize(new CountorModel()
            {
                BatchExecutedCount = batch,
                CurrentSum = 0,
                StartValue = 0,
                TotalCount = total
            }), correlationId, queueName, replyQueue);


            (int act, IModel channel, IConnection connection) = await TestHelpers.WaitForMessageResult(replyQueue, (message) => int.Parse(message));

            //assert
            act.Should().Be(expect);

            channel.Close();
            connection.Close();
        }
    }
}

