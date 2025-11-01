// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;

namespace IntegrationTester
{

    public class LongRunningBatchFixture : IDisposable
    {
        public MessageClient<Null> MessageClient { get; }
        public string QueueName { get; set; } = Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE") ?? "LongRunningBatchTaskQ";
        public LongRunningBatchFixture()
        {
            MessageClient = new MessageClient<Null>(new MessageClientOptions<Null>()
            {
                ProducerCfg = new ProducerConfig()
                {
                    BootstrapServers = EnvironmentVAR.HOSTNAME,
                    Acks = Acks.All,
                    EnableIdempotence = true,
                    // Add connection timeouts to prevent indefinite hangs
                    RequestTimeoutMs = 30000,      // 30 seconds for requests
                    MessageTimeoutMs = 60000,      // 60 seconds for message delivery
                    SocketTimeoutMs = 10000,       // 10 seconds for socket operations
                    MetadataMaxAgeMs = 30000,      // Refresh metadata every 30 seconds
                    // Reduce retries to fail faster in case of issues
                    MessageSendMaxRetries = 3,
                    RetryBackoffMs = 1000
                },
                ConsumerCfg = new ConsumerConfig()
                {
                    BootstrapServers = EnvironmentVAR.HOSTNAME,
                    GroupId = EnvironmentVAR.GROUPID,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                    // Add connection timeouts for consumer
                    SessionTimeoutMs = 30000,
                    SocketTimeoutMs = 10000,
                    MetadataMaxAgeMs = 30000
                },
                Topic = QueueName
            });
            Console.WriteLine("LongRunningBatchFixture setup");
        }

        public void Dispose()
        {
            MessageClient.Dispose();
            Console.WriteLine("LongRunningBatchFixture teardown");
        }
    }

    public class LongRunningBatchTaskTests : IClassFixture<LongRunningBatchFixture>
    {
        private readonly MessageClient<Null> _messageClient;

        public LongRunningBatchTaskTests(LongRunningBatchFixture fixture)
        {
            _messageClient = fixture.MessageClient;
        }
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
            string replyQueue = $"{Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_REPLYQUEUE")}_{correlationId}";
            await _messageClient.PublishMessageAsync(JsonSerializer.Serialize(new CountorModel()
            {
                BatchExecutedCount = batch,
                CurrentSum = 0,
                StartValue = 0,
                TotalCount = total
            }), correlationId, new Dictionary<string, string>() {
                { "TimeoutMilliseconds", "100"},
                { "ReplyTo", replyQueue }
            });

            int act = int.Parse(_messageClient.ConsumeMessage());

            //assert
            act.Should().Be(expect);
        }
    }
}

