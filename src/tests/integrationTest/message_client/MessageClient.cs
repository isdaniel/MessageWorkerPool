// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class MessageClient : MessageClientBase
{
    public MessageClient(
        MessageClientOptions options,
        ILogger logger) : base(options, logger)
    { }

    public string SendMessage(string routing,
        string intput,
        string correlationId = null,
        Dictionary<string, string> headers = null)
    {
        correlationId = this.PublishMessage(
           routing,
           Encoding.UTF8.GetBytes(intput),
           correlationId,
           headers);

        return correlationId;
    }
}

public class MessageClient<TInputMessage> : MessageClientBase
    where TInputMessage : class, new()
{
    public MessageClient(
        MessageClientOptions options,
        //TrackContext track,
        ILogger logger) : base(options, logger)
    {

    }

    public string SendMessage(string routing,
        TInputMessage intput,
        string correlationId = null,
        Dictionary<string, string> headers = null)
    {
        correlationId = this.PublishMessage(
           routing,
           Encoding.UTF8.GetBytes(JsonSerializer.Serialize(intput)),
           correlationId,
           headers);

        return correlationId;
    }
}
