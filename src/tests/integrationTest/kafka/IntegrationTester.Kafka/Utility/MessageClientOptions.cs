// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

public class MessageClientOptions<TKey>
{

    /// <summary>
    /// ConsumerConfig config
    /// </summary>
    public ConsumerConfig ConsumerCfg { get; set; }

    /// <summary>
    /// ProducerConfig config
    /// </summary>
    public ProducerConfig ProducerCfg { get; set; }

    public Action<ConsumerBuilder<TKey, string>> ConsumerRegister { get; set; } = (builder) => { };
    public Action<ProducerBuilder<TKey, string>> ProducerRegister { get; set; } = (builder) => { };

    public IConsumer<TKey, string> GetConsumer()
    {

        if (ConsumerRegister == null)
        {
            throw new ArgumentNullException(nameof(ConsumerRegister));
        }
        var builder = new ConsumerBuilder<TKey, string>(ConsumerCfg);
        ConsumerRegister(builder);
        return builder.Build();
    }

    public IProducer<TKey, string> GetProducer()
    {

        if (ProducerRegister == null)
        {
            throw new ArgumentNullException(nameof(ProducerRegister));
        }
        var builder = new ProducerBuilder<TKey, string>(ProducerCfg);
        ProducerRegister(builder);
        return builder.Build();
    }
    public string Topic { get; set; }
}
