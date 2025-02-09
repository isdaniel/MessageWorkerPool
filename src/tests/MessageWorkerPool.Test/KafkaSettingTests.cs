using Confluent.Kafka;
using FluentAssertions;
using MessageWorkerPool.KafkaMq;

namespace MessageWorkerPool.Test
{
    public class KafkaSettingTests
    {
        [Fact]
        public void GetConnectionString_WithValidConsumerConfig_ReturnsBootstrapServers()
        {
            var setting = new KafkaSetting<string>
            {
                ConsumerCfg = new ConsumerConfig { BootstrapServers = "localhost:9092" }
            };

            var connectionString = setting.GetConnectionString();

            connectionString.Should().Be("localhost:9092");
        }

        [Fact]
        public void GetConnectionString_WithNullConsumerConfig_ThrowsArgumentNullException()
        {
            var setting = new KafkaSetting<string>();

            Action act = () => setting.GetConnectionString();

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetConsumer_WithNullConsumerConfig_ThrowsArgumentNullException()
        {
            var setting = new KafkaSetting<string>();

            Action act = () => setting.GetConsumer();

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetConsumer_WithNullConsumerRegister_ThrowsArgumentNullException()
        {
            var setting = new KafkaSetting<string>
            {
                ConsumerCfg = new ConsumerConfig(),
                ConsumerRegister = null
            };

            Action act = () => setting.GetConsumer();

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetProducer_WithValidConfig_ReturnsProducer()
        {
            var setting = new KafkaSetting<string>
            {
                ProducerCfg = new ProducerConfig()
            };

            var producer = setting.GetProducer();

            producer.Should().NotBeNull();
        }

        [Fact]
        public void GetProducer_WithNullProducerConfig_ThrowsArgumentNullException()
        {
            var setting = new KafkaSetting<string>();

            Action act = () => setting.GetProducer();

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetProducer_WithNullProducerRegister_ThrowsArgumentNullException()
        {
            var setting = new KafkaSetting<string>
            {
                ProducerCfg = new ProducerConfig(),
                ProducerRegister = null
            };

            Action act = () => setting.GetProducer();

            act.Should().Throw<ArgumentNullException>();
        }
    }

}
