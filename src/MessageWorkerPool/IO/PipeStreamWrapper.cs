using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MessagePack;

namespace MessageWorkerPool.IO
{

    /// <summary>
    /// Wraps a <see cref="PipeStream"/> object to read and write .NET CLR objects.
    /// </summary>
    public class PipeStreamWrapper : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Gets the underlying <c>PipeStream</c> object.
        /// </summary>
        public PipeStream BaseStream { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the <see cref="BaseStream"/> object is connected or not.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the <see cref="BaseStream"/> object is connected; otherwise, <c>false</c>.
        /// </returns>
        public bool IsConnected
        {
            get { return BaseStream.IsConnected && _isConnected; }
        }

        private bool _isConnected;

        /// <summary>
        ///     Gets a value indicating whether the current stream supports read operations.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the stream supports read operations; otherwise, <c>false</c>.
        /// </returns>
        public bool CanRead
        {
            get { return BaseStream.CanRead; }
        }

        /// <summary>
        ///     Gets a value indicating whether the current stream supports write operations.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the stream supports write operations; otherwise, <c>false</c>.
        /// </returns>
        public bool CanWrite
        {
            get { return BaseStream.CanWrite; }
        }

        /// <summary>
        /// Constructs a new <c>PipeStreamWrapper</c> object that reads from and writes to the given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Stream to read from and write to</param>
        public PipeStreamWrapper(PipeStream stream)
        {
            BaseStream = stream;
            _isConnected = true;
        }

        /// <summary>
        /// Reads the next object from the pipe.  This method blocks until an object is sent or the pipe is disconnected.
        /// </summary>
        /// <returns>The next object read from the pipe, or <c>null</c> if the pipe disconnected.</returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TModel"/> is not marked as serializable.</exception>
        public virtual async Task<TModel> ReadAsync<TModel>() where TModel : class 
        {
            if (!CanRead)
            {
                return default(TModel);
            }

            var len = await ReadLengthAsync();

            if (len == 0)
            {
                return default(TModel);
            }

            var data = new byte[len];
            await BaseStream.ReadAsync(data, 0, len);
            return MessagePackSerializer.Deserialize<TModel>(data);
        }

        /// <summary>
        /// read data size length
        /// </summary>
        /// <returns>length of data size</returns>
        /// <exception cref="IOException"></exception>
        private async Task<int> ReadLengthAsync()
        {
            const int lenSize = sizeof(int);
            var lengthBuffer = new byte[lenSize];
            var bytesRead = await BaseStream.ReadAsync(lengthBuffer, 0, lenSize);

            if (bytesRead == 0)
            {
                _isConnected = false;
                return 0;
            }

            if (bytesRead != lenSize)
                throw new IOException($"Expected {lenSize} bytes but read {bytesRead}");

            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        }


        /// <summary>
        /// Writes an object to the pipe.  This method blocks until all data is sent.
        /// </summary>
        /// <param name="obj">Object to write to the pipe</param>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TModel"/> is not marked as serializable.</exception>
        public virtual async Task WriteAsync<TModel>(TModel obj)
            where TModel : class
        {
            var data = MessagePackSerializer.Serialize(obj);
            await WriteLengthAsync(data.Length);
            await BaseStream.WriteAsync(data, 0, data.Length);
            await BaseStream.FlushAsync();
        }


        /// <summary>
        /// write data length size in data stream beginning
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        private async Task WriteLengthAsync(int len)
        {
            var lenbuf = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len));
            await BaseStream.WriteAsync(lenbuf, 0, lenbuf.Length);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the current stream and releases any resources (such as sockets and file handles) associated with the current stream.
        /// </summary>
        /// <param name="disposing">Indicates whether managed resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (BaseStream.IsConnected)
            {
                BaseStream.Close();
            }

            _disposed = true;
        }

        public void WaitForPipeDrain() {
            BaseStream.WaitForPipeDrain();
        }
    }
}
