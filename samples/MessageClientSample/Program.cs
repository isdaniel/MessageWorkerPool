// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

Console.WriteLine("Hello, World!");



public class MessageClientOptions 
{
    public string ConnectionName = "default-connection";

    [Obsolete("Use ConnectionURL instead.")]
    public string HostName = "localhost";

    [Obsolete("Use ConnectionURL instead.")]
    public int Port = 5672;

    [Obsolete("Use ConnectionURL instead.")]
    public string VirtualHost = "/";

    [Obsolete("Use ConnectionURL instead.")]
    public string UserName = "guest";

    [Obsolete("Use ConnectionURL instead.")]
    public string Password = "guest";

    public string ConnectionURL { get; set; } = @"amqp://guest:guest@localhost:5672/";

    public enum MessageBusTypeEnum
    {
        EXCHANGE = 1,
        QUEUE = 2,
        NULL = 0
    }

    public MessageBusTypeEnum BusType { get; set; }
    public string QueueName = "default-queue";
    public bool QueueDurable = true;

    public string ExchangeName { get; set; }
    public string ExchangeType { get; set; }


    public int ConnectionRetryCount = 3;
    public TimeSpan ConnectionRetryTimeout = TimeSpan.FromSeconds(30);

    public TimeSpan? MessageExpirationTimeout = null;

    public readonly static MessageClientOptions Default = new MessageClientOptions();
}

public abstract class MessageClientBase : IDisposable
{
    protected MessageClientOptions _options { get; set; } = MessageClientOptions.Default;
    protected IServiceProvider _services { get; set; } = null;
    //protected TrackContext _track { get; private set; } = null;

    public MessageClientBase(
        MessageClientOptions options,
        //TrackContext track,
        IServiceProvider services,
        ILogger logger)
    {
        this._options = options;
        this._services = services;
        //this._track = track;

        var connfac = new ConnectionFactory()
        {
            Uri = new Uri(options.ConnectionURL)
        };

        this.connection = connfac.CreateConnection(this._options.ConnectionName);
        this.channel = this.connection.CreateModel();
    }

    protected IConnection connection = null;
    protected IModel channel = null;

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

        if (this._options.BusType == MessageClientOptions.MessageBusTypeEnum.QUEUE)
        {
            channel.QueueDeclare(
                //queue: routing,
                queue: this._options.QueueName,
                durable: this._options.QueueDurable,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }
        else if (this._options.BusType == MessageClientOptions.MessageBusTypeEnum.EXCHANGE)
        {
            channel.ExchangeDeclare(
                this._options.ExchangeName,
                this._options.ExchangeType,
                true,
                false,
                null);
        }
        else
        {
            throw new InvalidOperationException();
        }

        var trackHeaders = new Dictionary<string, string>();

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
            if (trackHeaders != null)
            {
                foreach (string key in trackHeaders.Keys)
                {
                    props.Headers[key] = Encoding.UTF8.GetBytes(trackHeaders[key] ?? "");
                }
            }

            props.CorrelationId = correlationId; //Guid.NewGuid().ToString("N");

            this.SetupMessageProperties(props);
        }

        if (this._options.BusType == MessageClientOptions.MessageBusTypeEnum.EXCHANGE)
        {
            channel.BasicPublish(
                exchange: this._options.ExchangeName ?? "",
                routingKey: routing,
                basicProperties: props,
                body: messageBody); //Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        }
        else if (this._options.BusType == MessageClientOptions.MessageBusTypeEnum.QUEUE)
        {
            channel.BasicPublish(
                exchange: "",
                routingKey: this._options.QueueName,
                basicProperties: props,
                body: messageBody); //Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        }

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

public class MessageClient : MessageClientBase
{
    public MessageClient(
        MessageClientOptions options,
        IServiceProvider services,
        ILogger logger) : base(options, services, logger)
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
        IServiceProvider services,
        ILogger logger) : base(options, services, logger)
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


public class MessageClient<TInputMessage, TOutputMessage> : MessageClientBase
    where TInputMessage : class,new()
    where TOutputMessage : class, new()
{
    public MessageClient(
        MessageClientOptions options,
        //TrackContext track,
        IServiceProvider services,
        ILogger logger) : base(options, services, logger)
    {
        this.ReplyQueueName = this.channel.QueueDeclare().QueueName;
        this.ReplyQueueConsumer = new EventingBasicConsumer(channel);
        this.ReplyQueueConsumer.Received += this.ReplyQueue_Received;
        this.channel.BasicConsume(this.ReplyQueueName, false, this.ReplyQueueConsumer);
    }

    private string ReplyQueueName = null;
    private EventingBasicConsumer ReplyQueueConsumer = null;

    protected override void SetupMessageProperties(IBasicProperties props)
    {
        base.SetupMessageProperties(props);
        props.ReplyTo = this.ReplyQueueName;
    }

    private void ReplyQueue_Received(object sender, BasicDeliverEventArgs e)
    {
        var body = e.Body.Span;
        var props = e.BasicProperties;
        TOutputMessage output = JsonSerializer.Deserialize<TOutputMessage>(
            Encoding.UTF8.GetString(body));

        lock (this._sync)
        {
            this.buffer[props.CorrelationId] = output;
            this.waitlist[props.CorrelationId].Set();
        }

        channel.BasicAck(
            deliveryTag: e.DeliveryTag,
            multiple: false);
    }

    private object _sync = new object();
    private Dictionary<string, TOutputMessage> buffer = new Dictionary<string, TOutputMessage>();
    private Dictionary<string, AutoResetEvent> waitlist = new Dictionary<string, AutoResetEvent>();

    public TOutputMessage SendMessage(string routing, TInputMessage input, Dictionary<string, string> headers)
    {
        return this.SendMessageAsync(routing, input, headers).Result;
    }


    public async Task<TOutputMessage> SendMessageAsync(string routing, TInputMessage input, Dictionary<string, string> headers)
    {
        string correlationId = this.PublishMessage(
            routing,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input)),
            null,
            headers);

        AutoResetEvent wait = new AutoResetEvent(false);
        lock (this._sync)
        {
            this.waitlist[correlationId] = wait;
        }

        await Task.Run(() => wait.WaitOne());

        lock (this._sync)
        {
            var output = this.buffer[correlationId];
            this.buffer.Remove(correlationId);
            this.waitlist.Remove(correlationId);

            return output;
        }
    }


    public override void Dispose()
    {
        this.ReplyQueueConsumer.Received -= this.ReplyQueue_Received;

        base.Dispose();
    }
}
