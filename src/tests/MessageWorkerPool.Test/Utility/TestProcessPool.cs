using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Moq;

namespace MessageWorkerPool.Test.Utility
{
    public class TestProcessPool : ProcessPool
    {
        public TestProcessPool(PoolSetting poolSetting, ILoggerFactory loggerFactory) : base(poolSetting, loggerFactory)
        {
            
        }

        public Mock<IProcessWrapper> mockProcess { get; internal set; }
        public Mock<StreamWriter> mockStandardInput { get; internal set; }

        internal override IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            mockStandardInput = new Mock<StreamWriter>(Stream.Null);
            var mockStandardOutput = new Mock<StreamReader>(Stream.Null);

            mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.WaitForExit());
            mockProcess.Setup(p => p.Close());
            mockProcess.Setup(x => x.StandardInput).Returns(mockStandardInput.Object);
            mockProcess.Setup(x => x.StandardOutput).Returns(mockStandardOutput.Object);
            mockProcess.Setup(x => x.Start()).Returns(true).Verifiable();
            return mockProcess.Object;
        }
    }
}
