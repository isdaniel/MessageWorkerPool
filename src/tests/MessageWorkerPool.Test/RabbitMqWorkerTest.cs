using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using MessageWorkerPool.Test.Utility;
using FluentAssertions.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.Test
{
    public class RabbitMqWorkerTest
    {
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<ILogger<RabbitMqWorker>> _loggerMock;
        private readonly Mock<IModel> _channel;

        public RabbitMqWorkerTest()
        {

            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerMock = new Mock<ILogger<RabbitMqWorker>>();
            _channel = new Mock<IModel>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
            //_channel.Setup();


        }


        //private RabbitMqWorkerTester CreateWorker()
        //{
        //    return CreateWorker(_channel);
        //}

        //private RabbitMqWorkerTester CreateWorker(PoolSetting setting)
        //{
        //    return CreateWorker(setting, _loggerFactoryMock.Object);
        //}
        private RabbitMqWorkerTester CreateWorker(RabbitMqSetting setting, WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory)
        {
            return new RabbitMqWorkerTester(setting,workerSetting, _channel.Object, loggerFactory);
        }

        [Fact]
        public async Task CreateWorker_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {

            Action act = () => CreateWorker(default(RabbitMqSetting), default(WorkerPoolSetting), _loggerFactoryMock.Object);
            act.Should().Throw<NullReferenceException>("*RabbitMqSetting*");

        }

        [Fact]
        public async Task CreateWorker_ShouldThrowArgumentNullException_WhenRabbiWorkerPoolSettingIsNull()
        {

            var rabbitMQSetting = new RabbitMqSetting() { };

            Action act = () => CreateWorker(rabbitMQSetting, default(WorkerPoolSetting), _loggerFactoryMock.Object);
            act.Should().Throw<NullReferenceException>("*WorkerPoolSetting*");

        }

        [Fact]
        public async Task CreateWorker_StartWorker_StartTimeIsOne_StartusIsRunning()
        {
            var mqSetting = new RabbitMqSetting();
            var workerSetting = new WorkerPoolSetting() { };
            var worker = CreateWorker(mqSetting, workerSetting, _loggerFactoryMock.Object);


            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            CancellationTokenSource cts = new CancellationTokenSource();
            await worker.InitWorkerAsync(cts.Token);
            worker.Status.Should().Be(WorkerStatus.Running);
            
            worker.mockProcess.Verify(x=> x.Start(),Times.Once);
            worker.mockProcess.Verify(x => x.BeginErrorReadLine(), Times.Once);
            worker.Process.Id.Should().Be(1);
        }

    }
}
