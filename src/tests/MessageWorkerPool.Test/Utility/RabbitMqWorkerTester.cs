using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Moq;
using MessageWorkerPool.RabbitMq;
using RabbitMQ.Client;

namespace MessageWorkerPool.Test.Utility
{
    public class RabbitMqWorkerTester : RabbitMqWorker
    {
        public RabbitMqWorkerTester(
            RabbitMqSetting setting,
            WorkerPoolSetting workerSetting,
            IModel channle,
            ILoggerFactory loggerFactory) : base(setting, workerSetting, channle, loggerFactory)
        {

        }

        public Mock<IProcessWrapper> mockProcess { get; internal set; }
        public Mock<StreamWriter> mockStandardInput { get; internal set; }

        protected override IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            mockStandardInput = new Mock<StreamWriter>(Stream.Null);
            var mockStandardOutput = new Mock<StreamReader>(Stream.Null);

            mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.WaitForExit());
            mockProcess.Setup(p => p.Close());
            mockProcess.Setup(x => x.StandardInput).Returns(mockStandardInput.Object);
            mockProcess.Setup(x => x.StandardOutput).Returns(mockStandardOutput.Object);
            mockProcess.Setup(x => x.Start()).Returns(true).Verifiable();
            mockProcess.Setup(x => x.Id).Returns(1);
            return mockProcess.Object;
        }
    }
}
