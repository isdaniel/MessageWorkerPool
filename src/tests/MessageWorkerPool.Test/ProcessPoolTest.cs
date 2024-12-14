using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Moq;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace MessageWorkerPool.Test
{
    public class ProcessPoolTest
    {
        private Mock<ILogger<ProcessPool>> _loggerMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private PoolSetting _poolSetting;

        public ProcessPoolTest()
        {
            _loggerMock = new Mock<ILogger<ProcessPool>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

            _poolSetting = new PoolSetting
            {
                WorkerUnitCount = 2,
                CommnadLine = "dummyCommand",
                Arguments = "--dummy"
            };
        }

        private ProcessPool CreateProcessPool()
        {
            return new TestProcessPool(_poolSetting, _loggerFactoryMock.Object);
        }

        [Fact]
        public void ProcessPool_ShouldInitializeCorrectNumberOfWorkers()
        {
            var processPool = CreateProcessPool();

            // Assert the number of workers and processes
            Assert.Equal(_poolSetting.WorkerUnitCount, processPool.ProcessCount);
        }

        [Fact]
        public async Task ProcessPool_ShouldAddTaskSuccessfully()
        {
            var processPool = CreateProcessPool();

            var messageTask = new MessageTask("Test Task",null,null,null);

            bool result = await processPool.AddTaskAsync(messageTask);

            Assert.True(result);
        }

        [Fact]
        public async Task ProcessPool_ShouldShutdownGracefully()
        {
            var processPool = CreateProcessPool();

            var cts = new CancellationTokenSource();
            await processPool.WaitFinishedAsync(cts.Token);
            cts.Cancel();
        }
    }

    public class TestProcessPool : ProcessPool
    {
        public TestProcessPool(PoolSetting poolSetting, ILoggerFactory loggerFactory) : base(poolSetting, loggerFactory)
        {
        }

        internal override IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            Mock<IProcessWrapper> _processMock = new Mock<IProcessWrapper>();
            _processMock.Setup(x => x.Start()).Returns(true);
            return _processMock.Object;
        }
    }
}
