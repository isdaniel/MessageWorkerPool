using System;
using Confluent.Kafka;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.KafkaMq
{
    /// <summary>
    /// Represents Kafka-specific settings and configurations for producers and consumers.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used in Kafka messages.</typeparam>
    public class KafkaSetting<TKey> : MqSettingBase
    {
        /// <summary>
        /// Gets the connection string (BootstrapServers) for Kafka.
        /// </summary>
        /// <returns>The BootstrapServers URL for Kafka connection.</returns>
        /// <exception cref="ArgumentNullException">Thrown if ConsumerCfg or its BootstrapServers property is null.</exception>
        public override string GetConnectionString()
        {
            if (ConsumerCfg == null || ConsumerCfg.BootstrapServers == null)
            {
                throw new ArgumentNullException(nameof(ConsumerCfg), "Consumer configuration or BootstrapServers cannot be null.");
            }

            return ConsumerCfg.BootstrapServers;
        }

        /// <summary>
        /// Kafka consumer configuration settings.
        /// </summary>
        public ConsumerConfig ConsumerCfg { get; set; }

        /// <summary>
        /// Kafka producer configuration settings.
        /// </summary>
        public ProducerConfig ProducerCfg { get; set; }

        /// <summary>
        /// Delegate for configuring the Kafka consumer builder before building the consumer instance.
        /// Allows customization of consumer settings.
        /// </summary>
        public Action<ConsumerBuilder<TKey, string>> ConsumerRegister { get; set; } = (builder) => { };

        /// <summary>
        /// Delegate for configuring the Kafka producer builder before building the producer instance.
        /// Allows customization of producer settings.
        /// </summary>
        public Action<ProducerBuilder<TKey, string>> ProducerRegister { get; set; } = (builder) => { };

        /// <summary>
        /// Creates and returns a configured Kafka consumer instance.
        /// </summary>
        /// <returns>A configured Kafka consumer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if ConsumerCfg or ConsumerRegister is null.</exception>
        public virtual IConsumer<TKey, string> GetConsumer()
        {
            if (ConsumerCfg == null)
            {
                throw new ArgumentNullException(nameof(ConsumerCfg), "Consumer configuration cannot be null.");
            }

            if (ConsumerRegister == null)
            {
                throw new ArgumentNullException(nameof(ConsumerRegister), "Consumer register delegate cannot be null.");
            }

            var builder = new ConsumerBuilder<TKey, string>(ConsumerCfg);
            ConsumerRegister(builder); // Apply any additional configuration via delegate.
            return builder.Build();
        }

        /// <summary>
        /// Creates and returns a configured Kafka producer instance.
        /// </summary>
        /// <returns>A configured Kafka producer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if ProducerCfg or ProducerRegister is null.</exception>
        public virtual IProducer<TKey, string> GetProducer()
        {
            if (ProducerCfg == null)
            {
                throw new ArgumentNullException(nameof(ProducerCfg), "Producer configuration cannot be null.");
            }

            if (ProducerRegister == null)
            {
                throw new ArgumentNullException(nameof(ProducerRegister), "Producer register delegate cannot be null.");
            }

            var builder = new ProducerBuilder<TKey, string>(ProducerCfg);
            ProducerRegister(builder); // Apply any additional configuration via delegate.
            return builder.Build();
        }
    }

}
