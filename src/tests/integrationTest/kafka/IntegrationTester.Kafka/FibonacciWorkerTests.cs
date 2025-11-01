// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;

namespace IntegrationTester
{
    public class FibonacciWorkerFixture : IDisposable
    {
        public MessageClient<Null> MessageClient { get; }
        public string QueueName { get; set; } = Environment.GetEnvironmentVariable("FIBONACCI_QUEUE") ?? "integrationTesting_fibonacciQ";
        public FibonacciWorkerFixture()
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
            Console.WriteLine("FibonacciWorkerFixture setup");
        }

        public void Dispose()
        {
            MessageClient.Dispose();
            Console.WriteLine("FibonacciWorkerFixture teardown");
        }
    }

    public class FibonacciWorkerTests : IClassFixture<FibonacciWorkerFixture>
    {
        private readonly MessageClient<Null> _messageClient;
        private readonly string _queueName;

        public FibonacciWorkerTests(FibonacciWorkerFixture fixture)
        {
            _messageClient = fixture.MessageClient;
            _queueName = fixture.QueueName;
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
            string replyQueue = $"{_queueName}_{correlationId}";

            await _messageClient.PublishMessageAsync(JsonSerializer.Serialize(new FibonacciModel()
            {
                Value = input
            }), correlationId, new Dictionary<string, string>() {
                { "ReplyTo", replyQueue }
            });

            int act = int.Parse(_messageClient.ConsumeMessage());

            //assert
            act.Should().Be(expect);
        }
    }
}

