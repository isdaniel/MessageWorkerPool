using System.Text.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using RabbitMQ.Client;

namespace IntegrationTester
{
    public class BalanceComparisonTests
    {
        private const int DefaultMessageCount = 10000;
        List<BalanceModel> expectedList = new List<BalanceModel>();

        [Fact]
        public async Task WorkerConsumeMessage_BalanceComparisonTest()
        {
            // Arrange
            var totalMessageCount = int.TryParse(Environment.GetEnvironmentVariable("TOTAL_MESSAGE_COUNT"), out int parsedValue) ?
                parsedValue : DefaultMessageCount;
            var queueName = Environment.GetEnvironmentVariable("BALANCEWORKER_QUEUE") ?? "integration-queue";
            var replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";

            CreateTestingData(totalMessageCount, queueName, replyQueueName);

            (ResponseMessage act, IModel channel, IConnection connection) = await TestHelpers.WaitForMessageResult(replyQueueName, (message) => JsonSerializer.Deserialize<ResponseMessage>(message));

            var actualList = (await GetAllBalanceFrom("public.act"));

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

        private void CreateTestingData(int totalMessageCount, string queueName, string replyQueueName)
        {
            using (MessageClient messageClient = new MessageClient(new MessageClientOptions()
            {
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
                Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort port) ? port : (ushort)5672,
                ExchangeName = Environment.GetEnvironmentVariable("EXCHANGENAME") ?? "integration-exchange"
            }))
            {
                Random rnd = new Random();
                int i = 1;
                while (i <= totalMessageCount) {
                    try
                    {
                        var model = new BalanceModel()
                        {
                            Balance = rnd.Next(1, 10000),
                            UserName = Guid.NewGuid().ToString("N")
                        };

                        messageClient.PublishMessage(queueName,
                            JsonSerializer.Serialize(model),
                            $"{Environment.MachineName}_{Guid.NewGuid().ToString("N")}",
                            new Dictionary<string, object>() { { "targetCount", totalMessageCount } },
                            i == totalMessageCount ? replyQueueName : string.Empty);
                        InsertUserBalance(model);
                        i++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to publish message {i}: {ex.Message}");
                    }
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

