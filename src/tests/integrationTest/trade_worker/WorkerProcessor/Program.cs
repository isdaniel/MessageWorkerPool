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

    class Program
    {
        static async Task Main(string[] args)
        {
            MessageProcessor processor = new MessageProcessor();
            await processor.DoWorkAsync(async (task) =>
            {
                var model = JsonSerializer.Deserialize<BalanceModel>(task.Message);
                await InsertOrUpdateUserBalanceAsync(model);
                return new MessageOutputTask()
                {
                    Message = $"Processing.. UserName:{model.UserName}, Balance:{model.Balance}",
                    Status = MessageStatus.MESSAGE_DONE
                };
            });
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
        static async Task<int> InsertOrUpdateUserBalanceAsync(BalanceModel model)
        {
            if (model == null)
            {
                throw new NullReferenceException(nameof(model));
            }

            using (var conn = new SqlConnection(GetEnvironmentVariable("DBConnection")))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var query = @"INSERT INTO dbo.Act (UserName,Balance) VALUES (@UserName, @Balance)";

                return await conn.ExecuteAsync(query, new
                {
                    model.UserName,
                    model.Balance
                }).ConfigureAwait(false);
            }
        }
    }

}

