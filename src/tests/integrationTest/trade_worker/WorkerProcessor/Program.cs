using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MessageWorkerPool.Utilities;
using Microsoft.Data.SqlClient;

namespace WorkerProcessSample
{
    public class BalanceModel
    {
        public string UserName { get; set; }
        public int Balance { get; set; }
    }

    public class ResponeMessage {
        public int ProcessCount { get; set; }
        public string Status { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            MessageProcessor processor = new MessageProcessor();
            await processor.DoWorkAsync(async (task) =>
            {
                var model = JsonSerializer.Deserialize<BalanceModel>(task.Message);
                var currentCount = await AddUserBalanceAndGetCountAsync(model);

                if (task.Headers is not null
                && task.Headers.TryGetValue("targetCount",out var obj)
                && int.TryParse(obj.ToString(), out var targetCount)
                && targetCount == currentCount)
                {
                    return new MessageOutputTask()
                    {
                        Message = JsonSerializer.Serialize(new ResponeMessage() {
                            ProcessCount = currentCount,
                            Status = "OK!"
                        }),
                        Status = MessageStatus.MESSAGE_DONE_WITH_REPLY
                    };
                }
                return new MessageOutputTask()
                {
                    Message = $"Processing.. UserName:{model.UserName}, Balance:{model.Balance}",
                    Status = MessageStatus.MESSAGE_DONE
                };


            }).ConfigureAwait(false);
        }
        static string GetEnvironmentVariable(string key, string defaultValue = null)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value) && defaultValue == null)
            {
                throw new InvalidOperationException($"Environment variable '{key}' is not set.");
            }
            return value ?? defaultValue;
        }
        static async Task<int> AddUserBalanceAndGetCountAsync(BalanceModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            using (var conn = new SqlConnection(GetEnvironmentVariable("DBConnection")))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                var query = @"
INSERT INTO dbo.Act (UserName, Balance) VALUES (@UserName, @Balance);

SELECT COUNT(*) AS CurrentCount
FROM dbo.Act;";

                // Use QuerySingleAsync or QuerySingleOrDefaultAsync to retrieve the count
                return await conn.QuerySingleAsync<int>(query, new
                {
                    model.UserName,
                    model.Balance
                }).ConfigureAwait(false);
            }
        }
    }

}

