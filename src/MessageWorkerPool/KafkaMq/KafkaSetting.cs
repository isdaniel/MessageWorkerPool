using System;
using Confluent.Kafka;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.KafkaMq
{
    public class KafkaSetting<TKey> : MqSettingBase
    {
        /// <summary>
        /// The uri to use for the connection.
        /// </summary>
        /// <returns></returns>
        public override string GetConnectionString()
        {
            if (ConsumerCfg == null || ConsumerCfg.BootstrapServers == null)
            {
                throw new ArgumentNullException(nameof(ConsumerCfg));
            }

            return ConsumerCfg.BootstrapServers;
        }

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

        public virtual IConsumer<TKey, string> GetConsumer()
        {
            if (ConsumerCfg == null)
            {
                throw new ArgumentNullException(nameof(ConsumerCfg));
            }

            if (ConsumerRegister == null)
            {
                throw new ArgumentNullException(nameof(ConsumerRegister));
            }
            var builder = new ConsumerBuilder<TKey, string>(ConsumerCfg);
            ConsumerRegister(builder);
            return builder.Build();
        }

        public virtual IProducer<TKey, string> GetProducer()
        {
            if (ProducerCfg == null)
            {
                throw new ArgumentNullException(nameof(ProducerCfg));
            }

            if (ProducerRegister == null)
            {
                throw new ArgumentNullException(nameof(ProducerRegister));
            }
            var builder = new ProducerBuilder<TKey, string>(ProducerCfg);
            ProducerRegister(builder);
            return builder.Build();
        }
    }
}
