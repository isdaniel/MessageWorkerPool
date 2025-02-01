// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

public class MessageClient<TKey> : IDisposable
{
    protected MessageClientOptions<TKey> _options { get; set; }
    private readonly IProducer<TKey, string> _producer;
    private readonly IConsumer<TKey, string> _consumer;
    private readonly IAdminClient _adminClient;
    public MessageClient(
        MessageClientOptions<TKey> options)
    {
        this._options = options;

        _producer = _options.GetProducer();
        _consumer = _options.GetConsumer();
        _adminClient = new AdminClientBuilder(config: new AdminClientConfig { BootstrapServers = EnvironmentVAR.HOSTNAME, Acks = Acks.All }).Build();
    }

    public virtual async ValueTask<string> PublishMessageAsync(
        string messageBody,
        string? correlationId = null,
        Dictionary<string, string>? messageHeaders = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        var replyHeaders = default(Headers);
        if (messageHeaders != null)
        {
            replyHeaders = new Headers();
            foreach (var item in messageHeaders)
            {
                replyHeaders.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));

                if (item.Key == "ReplyTo" && !string.IsNullOrWhiteSpace(item.Value))
                {
                    await CreateTopic(item.Value);

                    _consumer.Subscribe(item.Value);
                }
            }
        }

        await _producer.ProduceAsync(_options.Topic, new Message<TKey, string>()
        {
            Value = messageBody,
            Headers = replyHeaders
        }).ConfigureAwait(false);

        return correlationId;
    }

    public string ConsumeMessage()
    {
        var responseMsg = _consumer.Consume();
        var res = responseMsg.Message.Value;
        _consumer.Commit(responseMsg);
        return res;
    }

    public async Task CreateTopic(string topic)
    {
        try
        {
            await _adminClient.CreateTopicsAsync(new TopicSpecification[]
        {
            new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1
            }
        }).ConfigureAwait(false);
        }
        catch (CreateTopicsException ex) when (
           ex.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
        {

            //ignore
        }
    }

    public async Task DeleteTopics(string[] topics)
    {
        await _adminClient.DeleteTopicsAsync(topics).ConfigureAwait(false);
    }

    public virtual void Dispose()
    {
        _producer.Dispose();
        _consumer.Dispose();
        _adminClient.Dispose();
    }
}
