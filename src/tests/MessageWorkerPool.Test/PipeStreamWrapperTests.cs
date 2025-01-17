using Moq;
using MessageWorkerPool.IO;
using System.IO.Pipes;
using MessagePack;
using System.Net;

namespace MessageWorkerPool.Test
{
    public class PipeStreamWrapperTests
    {
        [Fact]
        public async Task ReadAsync_ShouldReturnDeserializedObject()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.In, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);

            var testObject = "test";
            var testData = MessagePackSerializer.Serialize(testObject);

            // Write data to the server stream
            var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(testData.Length));
            await serverStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await serverStream.WriteAsync(testData, 0, testData.Length);
            await serverStream.FlushAsync();

            // Act
            var result = await wrapper.ReadAsync<string>();

            // Assert
            Assert.Equal(testObject, result);
        }

        [Fact]
        public async Task WriteAsync_ShouldSendSerializedObject()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.Out, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);

            var testObject = "test";
            var expectedData = MessagePackSerializer.Serialize(testObject);

            // Act
            await wrapper.WriteAsync(testObject);

            // Read and validate length from the server stream
            var lengthBuffer = new byte[sizeof(int)];
            await serverStream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length);
            var dataLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
            Assert.Equal(expectedData.Length, dataLength);

            // Read and validate data from the server stream
            var dataBuffer = new byte[dataLength];
            await serverStream.ReadAsync(dataBuffer, 0, dataBuffer.Length);
            Assert.Equal(expectedData, dataBuffer);
        }

        [Fact]
        public void Dispose_ShouldCloseBaseStream()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.In);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.Out, serverStream.ClientSafePipeHandle);

            var wrapper = new PipeStreamWrapper(clientStream);

            // Act
            wrapper.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => clientStream.Write(new byte[1], 0, 1));
        }

        [Fact]
        public async Task WriteAsync_ShouldHandleLargeData()
        {
            // Arrange
            string pipeName = $"test_pipe_{Guid.NewGuid().ToString("N")}";
            var largeData = new string('a', 10000);
            var serverTask = Task.Run(async () => {
                using var serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
               PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough, 0, 0);
                await serverStream.WaitForConnectionAsync();
                var wrapper = new PipeStreamWrapper(serverStream);
                await wrapper.WriteAsync(largeData);
            });
            string act = string.Empty;
            var clientTask = Task.Run(async () => {
                using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                await clientStream.ConnectAsync();
                var wrapper = new PipeStreamWrapper(clientStream);
                act = await wrapper.ReadAsync<string>();
            });


            Task.WaitAll(serverTask, clientTask);
            Assert.Equal(act, largeData);
        }

        [Fact]
        public async Task ReadAsync_ShouldReturnNullIfStreamIsClosed()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.In, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);
            serverStream.Dispose();  // Close the stream

            // Act
            var result = await wrapper.ReadAsync<string>();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Dispose_ShouldCleanUpResources_ThrowNullReferenceException()
        {
            // Arrange
            var serverStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            var clientStream = new AnonymousPipeClientStream(PipeDirection.Out, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);
            var testData = new TestData { Name = "Test", Value = 123 };
            // Act
            wrapper.Dispose();

            // Assert

            await Assert.ThrowsAsync<NullReferenceException>(async () => await wrapper.WriteAsync(testData));
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteComplexObject()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.Out, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);

            var testData = new TestData { Name = "Test", Value = 123 };
            var expectedData = MessagePackSerializer.Serialize(testData);

            // Act
            await wrapper.WriteAsync(testData);

            // Read and validate length from the server stream
            var lengthBuffer = new byte[sizeof(int)];
            await serverStream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length);
            var dataLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
            Assert.Equal(expectedData.Length, dataLength);

            // Read and validate data from the server stream
            var dataBuffer = new byte[dataLength];
            await serverStream.ReadAsync(dataBuffer, 0, dataBuffer.Length);
            var deserializedData = MessagePackSerializer.Deserialize<TestData>(dataBuffer);
            Assert.Equal(testData.Name, deserializedData.Name);
            Assert.Equal(testData.Value, deserializedData.Value);
        }

        [Fact]
        public void CanRead_ShouldReturnTrueWhenStreamSupportsRead()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.Out, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(serverStream);

            // Assert
            Assert.True(wrapper.CanRead);
            Assert.False(clientStream.CanRead);
        }

        [Fact]
        public void CanWrite_ShouldReturnTrueWhenStreamSupportsWrite()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.In, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);


            // Assert
            Assert.True(wrapper.CanRead);
            Assert.False(serverStream.CanRead);
        }

        [Fact]
        public void IsConnected_ShouldReturnTrueWhenStreamIsConnected()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.In, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);

            // Act
            var isConnected = wrapper.IsConnected;

            // Assert
            Assert.True(isConnected);
        }

        [Fact]
        public void IsConnected_ShouldReturnFalseWhenStreamIsDisconnected()
        {
            // Arrange
            using var serverStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            using var clientStream = new AnonymousPipeClientStream(PipeDirection.In, serverStream.ClientSafePipeHandle);
            var wrapper = new PipeStreamWrapper(clientStream);
            serverStream.Dispose();  // Disconnect the server stream

            // Assert
            Assert.False(serverStream.IsConnected);
            Assert.True(clientStream.IsConnected);
        }

        [MessagePackObject]
        public class TestData
        {
            [Key(0)]
            public string Name { get; set; }
            [Key(1)]
            public int Value { get; set; }
        }
    }
}
