// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public class MessageClientOptions
{
    /// <summary>
    /// The uri to use for the connection.
    /// </summary>
    /// <returns></returns>
    public Uri GetUri()
    {
        return new Uri(GetConnectionString());
    }

    public string? GetConnectionString()
    {
        return $"amqp://{UserName}:{Password}@{HostName}:{Port}";
    }

    /// <summary>
    /// Rabbit Mq Port
    /// </summary>
    public ushort Port { get; set; }
    public string? QueueName { get; set; }
    public string? UserName { get; set; }
    /// <summary>
    /// Password to use when authenticating to the server.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The host to connect to
    /// </summary>
    public string? HostName { get; set; }
    public string? ExchangeName { get; set; }

    public readonly static MessageClientOptions Default = new MessageClientOptions();
}
