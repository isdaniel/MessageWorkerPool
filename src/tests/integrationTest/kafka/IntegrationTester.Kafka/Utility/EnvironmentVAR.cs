// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public static class EnvironmentVAR
{
    public static readonly string HOSTNAME = Environment.GetEnvironmentVariable("KAFKA_HOSTNAME") ?? "localhost:9092";
    public static readonly string GROUPID = Environment.GetEnvironmentVariable("KAFKA_GROUPID") ?? "integrationGroup";
}
