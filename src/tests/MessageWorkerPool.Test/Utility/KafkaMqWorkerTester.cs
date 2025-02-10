using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Moq;
using MessageWorkerPool.IO;
using MessageWorkerPool.KafkaMq;
using Confluent.Kafka;
using FluentAssertions;

namespace MessageWorkerPool.Test.Utility
{
    internal class KafkaMqWorkerTester : KafkaMqWorker<Null>
    {
        
        internal bool GracefulReleaseCalled;

        public KafkaMqWorkerTester(WorkerPoolSetting workerSetting, KafkaSetting<Null> kafkaSetting, ILogger logger) : base(workerSetting, kafkaSetting, logger)
        {
        }

        internal Mock<IProcessWrapper> mockProcess { get; set; }
        internal Mock<StreamWriter> mockStandardInput { get; set; }
        internal Mock<StreamReader> mockStandardOutput { get; set; }
        internal Mock<PipeStreamWrapper> pipeStream { get; set; }

        protected override IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            mockStandardInput = new Mock<StreamWriter>(Stream.Null);
            mockStandardOutput = new Mock<StreamReader>(Stream.Null);

            mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.WaitForExit());
            mockProcess.Setup(p => p.WaitForExit(It.IsAny<int>())).Returns(true);
            mockProcess.Setup(p => p.Close());
            mockProcess.Setup(x => x.StandardInput).Returns(mockStandardInput.Object);
            mockProcess.Setup(x => x.StandardOutput).Returns(mockStandardOutput.Object);
            mockProcess.Setup(x => x.Start()).Returns(true).Verifiable();
            mockProcess.Setup(x => x.Id).Returns(1);
            return mockProcess.Object;
        }

        protected override Task<PipeStreamWrapper> CreateOperationPipeAsync(string pipeName, CancellationToken token = default)
        {
            pipeStream = new Mock<PipeStreamWrapper>(null);
            return Task.FromResult(pipeStream.Object);
        }

        protected override async Task GracefulReleaseAsync(CancellationToken token)
        {
            GracefulReleaseCalled = true; // Mark as executed
            await base.GracefulReleaseAsync(token).ConfigureAwait(false);
        }
        public async Task<PipeStreamWrapper> TestCreateOperationPipeAsync(string pipeName)
        {
            return await CreateOperationPipeAsync(pipeName);
        }

        protected override void SetupMessageQueueSetting(CancellationToken token)
        {
            base.SetupMessageQueueSetting(token);
            //to avoid unit test shutdown so quickly.
            Thread.Sleep(200);
        }

        public void VeirfyCreateProcess(ProcessStartInfo processStartInfo)
        {
            base.CreateProcess(processStartInfo).Should().NotBeNull();
        }
    }
}
