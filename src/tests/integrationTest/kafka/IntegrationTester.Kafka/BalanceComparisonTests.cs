using System.Text.Json;
using System.Text.RegularExpressions;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Dapper;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Npgsql;

namespace IntegrationTester
{
    public class BalanceComparisonFixture : IDisposable
    {
        public MessageClient<Null> MessageClient { get; }
        public string QueueName { get; set; } = Environment.GetEnvironmentVariable("BALANCEWORKER_QUEUE") ?? "integration-queue";
        public BalanceComparisonFixture()
        {
            MessageClient = new MessageClient<Null>(new MessageClientOptions<Null>()
            {
                ProducerCfg = new ProducerConfig()
                {
                    BootstrapServers = EnvironmentVAR.HOSTNAME,
                    Acks = Acks.All,
                    EnableIdempotence = true
                },
                ConsumerCfg = new ConsumerConfig()
                {
                    BootstrapServers = EnvironmentVAR.HOSTNAME,
                    GroupId = EnvironmentVAR.GROUPID,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                },
                Topic = QueueName
            });
            Console.WriteLine("BalanceComparisonFixture setup");
        }

        public void Dispose()
        {
            MessageClient.Dispose();
            Console.WriteLine("BalanceComparisonFixture teardown");
        }
    }

    public class BalanceComparisonTests : IClassFixture<BalanceComparisonFixture>
    {
        private const int DefaultMessageCount = 10000;
        List<BalanceModel> expectedList = new List<BalanceModel>();

        private readonly MessageClient<Null> _messageClient;
        private readonly string _queueName;

        public BalanceComparisonTests(BalanceComparisonFixture fixture)
        {
            _messageClient = fixture.MessageClient;
            _queueName = fixture.QueueName;
        }


        [Fact]
        public async Task WorkerConsumeMessage_BalanceComparisonTest()
        {
            // Arrange
            var totalMessageCount = int.TryParse(Environment.GetEnvironmentVariable("TOTAL_MESSAGE_COUNT"), out int parsedValue) ?
                parsedValue : DefaultMessageCount;
            var replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";

            await CreateTestingData(totalMessageCount, _queueName, replyQueueName);
            Console.WriteLine("CreateTestingData done, all message pushed to message queue.");
            ResponseMessage act = JsonSerializer.Deserialize<ResponseMessage>(_messageClient.ConsumeMessage()); 

            var actualList = await GetAllBalanceFrom("public.act");

            // Assert
            expectedList.Count.Should().Be(totalMessageCount);
            ValidateBalanceComparison(actualList.ToList(), expectedList);
            act.Status.Should().Be("OK!");
        }

        private static async Task<IEnumerable<BalanceModel>> GetAllBalanceFrom(string tableName)
        {
            using var conn = new NpgsqlConnection(Environment.GetEnvironmentVariable("DBConnection"));
            await conn.OpenAsync().ConfigureAwait(false);
            return await conn.QueryAsync<BalanceModel>($"SELECT UserName, Balance FROM {tableName}").ConfigureAwait(false);
        }

        private async Task CreateTestingData(int totalMessageCount, string queueName, string replyQueueName)
        {
            
            Random rnd = new Random();
            int i = 1;
            while (i <= totalMessageCount)
            {
                try
                {
                    var model = new BalanceModel()
                    {
                        Balance = rnd.Next(1, 10000),
                        UserName = Guid.NewGuid().ToString("N")
                    };

                        await _messageClient.PublishMessageAsync(JsonSerializer.Serialize(model),
                        $"{Environment.MachineName}_{Guid.NewGuid().ToString("N")}",
                        new Dictionary<string, string>() {
                            { "targetCount", totalMessageCount.ToString() },
                            { "ReplyTo" , i == totalMessageCount ? replyQueueName : string.Empty}
                        }).ConfigureAwait(false);
                    InsertUserBalance(model);
                    i++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to publish message {i}: {ex.Message}");
                }
            }
            
        }

        private static void ValidateBalanceComparison(List<BalanceModel> actList, List<BalanceModel> expectList)
        {
            expectList.Count.Should().Be(actList.Count);
            expectList.Should().BeEquivalentTo(actList, options => options.WithoutStrictOrdering());
        }
        void InsertUserBalance(BalanceModel model)
        {
            if (model == null)
            {
                throw new NullReferenceException(nameof(model));
            }

            expectedList.Add(model);
        }
    }
}

