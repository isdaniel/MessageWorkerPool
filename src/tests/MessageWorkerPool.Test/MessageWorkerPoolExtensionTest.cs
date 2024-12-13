using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;

namespace MessageWorkerPool.Test
{
    public class MessageWorkerPoolExtensionTest
    {
        [Fact]
        public void AddRabbitMqWorkerPool_ShouldRegisterRabbitMqSettingInServiceProvider()
        {
            var services = new ServiceCollection();
            var rabbitMQSetting = new RabbitMqSetting(){
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                QueueName = "queue1",
                UserName = "user1",
                PrefetchTaskCount = 1,
                PoolSettings = new PoolSetting []{
                    new PoolSetting(){
                        Arguments = "client.dll",
                        Group = "groupA",
                        WorkUnitCount = 1,
                        CommnadLine = "dotnet"
                    }
                }
            };

            services.AddRabbiMqWorkerPool(rabbitMQSetting);

            using var serviceProvider = services.BuildServiceProvider();

            var expectSetting = serviceProvider.GetService<RabbitMqSetting>();

            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(rabbitMQSetting);
        }


        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {
            var services = new ServiceCollection();
            var rabbitMQSetting = default(RabbitMqSetting);

            Action act = () => services.AddRabbiMqWorkerPool(rabbitMQSetting);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
