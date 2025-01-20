import asyncio
import msgpack
import time
import socket
from asyncio import StreamReader, StreamWriter
from typing import Callable, Optional, Dict, Tuple, Awaitable
from dataclasses import dataclass, asdict

class MessageCommunicate:
    """Signals for communication."""
    CLOSED_SIGNAL = "__quit__"
    INTERRUPT_SIGNAL = "__context_switch__"

class MessageStatus:
    """Message status codes."""
    IgnoreMessage = -1
    MessageDone = 200
    MessageDoneWithReply = 201

@dataclass
class MessageOutputTask:
    message: str
    status: int
    headers: Optional[Dict[str, str]] = None
    reply_queue_name: Optional[str] = None

    def to_serializable(self):
        return {
            '0': self.message,
            '1': self.status,
            '2': self.headers if self.headers is None else dict(self.headers),
            '3': self.reply_queue_name
        }

@dataclass
class MessageInputTask:
    message: str
    correlation_id: str
    original_queue_name: str
    headers: Optional[Dict[str, str]] = None

    @classmethod
    def from_serialized(cls, data: Dict) -> "MessageInputTask":
        """Deserialize the data into a MessageInputTask."""
        return cls(
            message=data.get('0'),
            correlation_id=data.get('1'),
            original_queue_name=data.get('2'),
            headers=data.get('3', {}),
        )

class MessageProcessor:
    def __init__(self, stream: Tuple[StreamReader, StreamWriter]):
        self.stream_reader, self.stream_writer = stream
        self.close_token = asyncio.Event()
        self.task: Optional[asyncio.Task] = None



    async def write_async(self, obj: any):
        """Serialize and write a model to the stream."""
        serializable_obj = obj.to_serializable() if hasattr(obj, 'to_serializable') else obj
        data = msgpack.packb(serializable_obj, use_bin_type=True)
        data_len = len(data).to_bytes(4, 'big')
        # print(f"data is [{','.join(str(x) for x in data)}]")
        # print(f"data_len is [{','.join(str(x) for x in data_len)}]")
        self.stream_writer.write(data_len)
        self.stream_writer.write(data)
        await self.stream_writer.drain()

    async def read_async(self, model_type) -> any:
        """Read and deserialize a model from the stream."""
        size_buffer = await self.stream_reader.readexactly(4)
        #print(f"size_buffer is [{','.join(str(x) for x in size_buffer)}]")
        data_size = int.from_bytes(size_buffer, 'big')
        data_buffer = await self.stream_reader.readexactly(data_size)
        data = msgpack.unpackb(data_buffer, raw=False)
        return model_type.from_serialized(data)

    def initial(self):
        loop = asyncio.get_event_loop()
        self.task = loop.create_task(self._close_on_signal())

    async def _close_on_signal(self):
        """Listen for the close signal."""
        loop = asyncio.get_event_loop()
        while not self.close_token.is_set():
            line = await loop.run_in_executor(None, input)
            if line.strip() == MessageCommunicate.CLOSED_SIGNAL:
                self.close_token.set()

    async def do_work_async(self, process: Callable[[MessageInputTask], Awaitable[MessageOutputTask]]):
        """Process tasks asynchronously."""
        print("Worker starting...")
        print("Enter text 'quit' to stop:")
        while not self.close_token.is_set():
            try:
                task = await self.read_async(MessageInputTask)
                result = await process(task)
                await self.write_async(result)
            except Exception as e:
                print(f"Error: {e}")
                break

        print("Exiting...")
        if self.task:
            await self.task

# Example usage:
async def example_process(input_task: MessageInputTask) -> MessageOutputTask:
    """Example process function."""
    print(f"example_process Processing: {input_task.message}, wait for 500ms", flush=True)
    await asyncio.sleep(0.5)
    return MessageOutputTask(
        message=f"Processed: {input_task.message}",
        status=MessageStatus.MessageDone,
        headers=input_task.headers,
        reply_queue_name=None
    )

async def main():
    pipe_name = input("Enter pipe name: ")
    print(f"pipe_name :{pipe_name}")
    pipe_name = f"/tmp/CoreFxPipe_{pipe_name}"
    client_socket = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    try:
        print(f"Connecting to UNIX domain socket: {pipe_name}")
        client_socket.connect(pipe_name)
        print(f"Connected to socket: {pipe_name}")
        reader, writer = await asyncio.open_unix_connection(sock=client_socket)
        processor = MessageProcessor((reader, writer))
        processor.initial()
        await processor.do_work_async(example_process)
    except Exception as e:
        print(f"An error occurred: {e}")

if __name__ == "__main__":
    asyncio.run(main())
