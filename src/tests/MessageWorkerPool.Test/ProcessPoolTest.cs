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

namespace MessageWorkerPool.Test
{
    public class ProcessPoolTest
    {
        private readonly Mock<ILogger<ProcessPool>> _loggerMock;
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly PoolSetting _poolSetting;

        public ProcessPoolTest()
        {
            _loggerMock = new Mock<ILogger<ProcessPool>>();

            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

            _poolSetting = new PoolSetting
            {
                WorkerUnitCount = 3,
                CommnadLine = "dummyCommand",
                Arguments = "--dummy"
            };
        }


        private TestProcessPool CreateProcessPool()
        {
            return CreateProcessPool(_poolSetting);
        }

        private TestProcessPool CreateProcessPool(PoolSetting setting)
        {
            return new TestProcessPool(setting, _loggerFactoryMock.Object);
        }

        [Fact]
        public void ProcessPool_ShouldInitializeCorrectNumberOfWorkers()
        {
            var processPool = CreateProcessPool();

            processPool.ProcessCount.Should().Be(_poolSetting.WorkerUnitCount);
        }

        [Fact]
        public async Task ProcessPool_ShouldAddTaskSuccessfully()
        {
            var processPool = CreateProcessPool();

            var messageTask = new MessageTask("Test Task",null,null,null);

            bool result = await processPool.AddTaskAsync(messageTask);

            result.Should().BeTrue();
        }


        [Fact]
        public async Task ProcessPool_WaitFinishedAsync_ShouldAddTaskFailed()
        {
            var processPool = CreateProcessPool();
            await processPool.WaitFinishedAsync(CancellationToken.None);
            
            var messageTask = new MessageTask("Test Task", null, null, null);
            bool result = await processPool.AddTaskAsync(messageTask);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessPool_ShouldShutdownGracefully()
        {
            var processPool = CreateProcessPool();

            await processPool.WaitFinishedAsync(CancellationToken.None);

            processPool.IsFinish.Should().BeTrue();
        }

        [Fact]
        public void Should_InitializeProcessPool_WithCorrectWorkerUnitCount()
        {
            var processPool = CreateProcessPool(new PoolSetting() {
                WorkerUnitCount = 5,
                CommnadLine = "dummyCommand"
            });

            processPool.ProcessCount.Should().Be(5);
        }

        [Fact]
        public void Should_ThrowArgumentNullException_WhenCommandLineIsEmpty()
        {

            Action act = () => CreateProcessPool(new PoolSetting()
            {
                WorkerUnitCount = 5,
                CommnadLine = string.Empty
            });
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Should_ThrowNullReferenceException_WhenPoolSettingIsNull()
        {
            Action act = () => CreateProcessPool(null);
            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public async Task WaitFinishedAsync_ShouldFinishAllTasksAndSignalProcesses()
        {
            int unitCount = 1;
            var processPool = CreateProcessPool(new PoolSetting
            {
                WorkerUnitCount = (ushort)unitCount,
                CommnadLine = "dummyCommand",
                Arguments = "--dummy"
            });

            // Act
            var messageTask = new MessageTask("Test Task", null, null, null);
            var actJson = JsonSerializer.Serialize(messageTask);
            await processPool.AddTaskAsync(messageTask);
            await processPool.WaitFinishedAsync(CancellationToken.None);
            
            // Assert
            processPool.mockStandardInput.Verify(p => p.WriteLine(actJson), Times.Once);
            processPool.mockStandardInput.Verify(p => p.WriteLine(ProcessPool.CLOSED_SIGNAL), Times.Exactly(unitCount));
            processPool.mockProcess.Verify(p => p.WaitForExit(), Times.Exactly(unitCount));
            processPool.mockProcess.Verify(p => p.Close(), Times.Exactly(unitCount));
        }


        [Fact]
        public void Constructor_ShouldThrowNullReferenceException_WhenLogFactoryIsNull()
        {
            Action act = () => new ProcessPool(new PoolSetting
            {
                WorkerUnitCount = 1,
                CommnadLine = "dummyCommand",
                Arguments = "--dummy"
            }, null);
            act.Should().Throw<NullReferenceException>();
        }
    }
}
