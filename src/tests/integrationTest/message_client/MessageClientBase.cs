// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

public abstract class MessageClientBase : IDisposable
{
    protected MessageClientOptions _options { get; set; } = MessageClientOptions.Default;

    public MessageClientBase(
        MessageClientOptions options,
        ILogger logger)
    {
        this._options = options;

        var connfac = new ConnectionFactory()
        {
            Uri = options.GetUri()
        };

        this.connection = connfac.CreateConnection();
        this.channel = this.connection.CreateModel();
    }

    protected IConnection connection = null;
    protected IModel channel = null;

    public void InitialQueue() {
        channel.QueueDeclare(_options.QueueName, true, false, false, null);
        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, true, false, null);
        channel.QueueBind(_options.QueueName, _options.ExchangeName, "*");
    }

    protected virtual string PublishMessage(
        string routing,
        byte[] messageBody,
        string correlationId = null,
        Dictionary<string, string> messageHeaders = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        channel.QueueDeclare(
            queue: this._options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);


        IBasicProperties props = null;//this.PrepareMessageProperties(correlationId, messageHeaders);
        {
            props = channel.CreateBasicProperties();

            props.ContentType = "application/json";
            if (this._options.MessageExpirationTimeout != null)
            {
                props.Expiration = this._options.MessageExpirationTimeout.Value.TotalMilliseconds.ToString();
            }

            props.Headers = new Dictionary<string, object>();
            if (messageHeaders != null)
            {
                foreach (string key in messageHeaders.Keys)
                {
                    props.Headers[key] = Encoding.UTF8.GetBytes(messageHeaders[key] ?? "");
                }
            }

            props.CorrelationId = correlationId; //Guid.NewGuid().ToString("N");

            this.SetupMessageProperties(props);
        }

        channel.BasicPublish(
               exchange: _options.ExchangeName,
               routingKey: routing,
               basicProperties: props,
               body: messageBody);

        return correlationId;
    }

    protected virtual void SetupMessageProperties(IBasicProperties props)
    {
    }

    public virtual void Dispose()
    {
        this.channel.Dispose();
        this.connection.Dispose();
    }
}
