// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Channels;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;

public class GracefulShutdownTest
{
    private const int DefaultMessageCount = 10000;
    private const string HOST = "127.0.0.1";
    private const string ConnectionString = $"Data Source={HOST};Initial Catalog=orleans;User ID=sa;Password=test.123;TrustServerCertificate=true;";

    [Fact]
    public async Task WorkerConsumeMessage_BalanceComparisonTest()
    {
        //todo refactor test case and table name to be file.
        var totalMessageCount = GetEnvironmentVariableAsInt("TOTAL_MESSAGE_COUNT", DefaultMessageCount);
        var actList = (await GetAllBalanceFrom("dbo.Act")).ToList();

        // Retry logic for loading balances with a delay until the total count reaches the expected message count
        while (actList.Count < totalMessageCount)
        {
            Console.WriteLine($"dbo.Act current rows: {actList.Count}");
            await Task.Delay(5000);
            actList = (await GetAllBalanceFrom("dbo.Act")).ToList();
        }

        var expectList = (await GetAllBalanceFrom("dbo.Expect")).ToList();

        ValidateBalanceComparison(actList, expectList);
    }

    private static void ValidateBalanceComparison(List<BalanceModel> actList, List<BalanceModel> expectList)
    {
        expectList.Count.Should().Be(actList.Count);
        expectList.Should().BeEquivalentTo(actList, options => options.WithStrictOrdering());
    }

    private static async Task<IEnumerable<BalanceModel>> GetAllBalanceFrom(string tableName)
    {
        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        return await conn.QueryAsync<BalanceModel>($"SELECT UserName, Balance FROM {tableName}").ConfigureAwait(false);
    }

    private static ushort GetEnvironmentVariableAsUShort(string key, ushort defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return ushort.TryParse(value, out ushort parsedValue) ? parsedValue : defaultValue;
    }

    private static int GetEnvironmentVariableAsInt(string key, ushort defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out int parsedValue) ? parsedValue : defaultValue;
    }

    private static string GetEnvironmentVariable(string key, string defaultValue = null)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value) && defaultValue == null)
        {
            throw new InvalidOperationException($"Environment variable '{key}' is not set.");
        }
        return value ?? defaultValue;
    }


    public class BalanceModel
    {
        public string UserName { get; set; }
        public int Balance { get; set; }
    }

}


