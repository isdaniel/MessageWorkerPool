// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Moq;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;

namespace IntegrationTester
{
    [CollectionDefinition("BalanceComparison Collection")]
    public class BalanceComparisonCollection { }

    // Tests in BalanceComparison Collection
    [Collection("BalanceComparison Collection")]
    public class BalanceComparisonTests
    {
        private const int DefaultMessageCount = 10000;

        [Fact]
        public async Task WorkerConsumeMessage_BalanceComparisonTest()
        {
            // Arrange
            var totalMessageCount = int.TryParse(Environment.GetEnvironmentVariable("TOTAL_MESSAGE_COUNT"), out int parsedValue) ?
                parsedValue : DefaultMessageCount;
            var queueName = Environment.GetEnvironmentVariable("BALANCEWORKER_QUEUE") ?? "integration-queue";
            var replyQueueName = Environment.GetEnvironmentVariable("REPLY_QUEUE") ?? "integrationTesting_replyQ";

            await CreateTestingData(totalMessageCount, queueName, replyQueueName);

            (ResponseMessage act, IModel channel, IConnection connection) = await TestHelpers.WaitForMessageResult(replyQueueName, (message) => JsonSerializer.Deserialize<ResponseMessage>(message));

            var expectedList = (await GetAllBalanceFrom("dbo.Expect")).ToList();
            var actualList = (await GetAllBalanceFrom("dbo.Act")).ToList();

            // Assert
            expectedList.Count.Should().Be(totalMessageCount);
            ValidateBalanceComparison(actualList, expectedList);
            act.Status.Should().Be("OK!");
        }

        private static async Task<IEnumerable<BalanceModel>> GetAllBalanceFrom(string tableName)
        {
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("DBConnection"));
            await conn.OpenAsync().ConfigureAwait(false);
            return await conn.QueryAsync<BalanceModel>($"SELECT UserName, Balance FROM {tableName}").ConfigureAwait(false);
        }

        private Task CreateTestingData(int totalMessageCount, string queueName, string replyQueueName)
        {
            return Task.Factory.StartNew(async () =>
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
                    for (int i = 1; i <= totalMessageCount; i++)
                    {
                        Random rnd = new Random();
                        var model = new BalanceModel()
                        {
                            Balance = rnd.Next(1, 10000),
                            UserName = Guid.NewGuid().ToString("N")
                        };

                        messageClient.PublishMessage(queueName,
                                JsonSerializer.Serialize(model),
                                $"{Environment.MachineName}_{Guid.NewGuid().ToString("N")}",
                                new Dictionary<string, object>() {
                    { "targetCount",totalMessageCount}
                        }, i == totalMessageCount ? replyQueueName : string.Empty);

                        await InsertUserBalanceAsync(model);
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private static void ValidateBalanceComparison(List<BalanceModel> actList, List<BalanceModel> expectList)
        {
            expectList.Count.Should().Be(actList.Count);
            expectList.Should().BeEquivalentTo(actList, options => options.WithStrictOrdering());
        }
        async Task<int> InsertUserBalanceAsync(BalanceModel model)
        {
            if (model == null)
            {
                throw new NullReferenceException(nameof(model));
            }

            using (var conn = new SqlConnection(Environment.GetEnvironmentVariable("DBConnection")))
            {
                await conn.OpenAsync();
                return await conn.ExecuteAsync("INSERT INTO dbo.Expect (UserName,Balance) VALUES (@UserName, @Balance)", new
                {
                    model.UserName,
                    model.Balance
                });
            }
        }
    }
}

