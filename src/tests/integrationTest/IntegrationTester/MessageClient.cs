// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using RabbitMQ.Client;

public class MessageClient : IDisposable
{
    protected MessageClientOptions _options { get; set; } = MessageClientOptions.Default;

    public MessageClient(
        MessageClientOptions options)
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

    public virtual string PublishMessage(
        string routing,
        string messageBody,
        string correlationId = null,
        Dictionary<string, object> messageHeaders = null,
        string replyQueueName = null)
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


        IBasicProperties props = null;
        {
            props = channel.CreateBasicProperties();
            props.ContentType = "application/json";
            if (this._options.MessageExpirationTimeout != null)
            {
                props.Expiration = this._options.MessageExpirationTimeout.Value.TotalMilliseconds.ToString();
            }

            if (!string.IsNullOrWhiteSpace(replyQueueName))
            {
                props.ReplyTo = replyQueueName;
                channel.QueueDeclare(replyQueueName, true, false, false, null);
            }

            props.Headers = messageHeaders;

            props.CorrelationId = correlationId;
        }

        channel.BasicPublish(
                exchange: _options.ExchangeName,
                routingKey: routing,
                basicProperties: props,
                body: Encoding.UTF8.GetBytes(messageBody));

        return correlationId;
    }

    public virtual void Dispose()
    {
        this.channel.Dispose();
        this.connection.Dispose();
    }
}
