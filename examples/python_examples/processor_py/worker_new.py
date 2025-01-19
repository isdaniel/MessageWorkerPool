import asyncio
import msgpack
import os
from asyncio import StreamReader, StreamWriter
from typing import Callable, Optional, Dict, Tuple
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


@dataclass
class MessageInputTask:
    message: str
    correlation_id: str
    original_queue_name: str
    headers: Optional[Dict[str, str]] = None


class MessageProcessor:
    def __init__(self, stream: Tuple[StreamReader, StreamWriter]):
        self.stream_reader, self.stream_writer = stream
        self.close_token = asyncio.Event()
        self.task: Optional[asyncio.Task] = None

    async def read_async(self, model_type) -> any:
        """Read and deserialize a model from the stream."""
        try:
            size_buffer = await asyncio.wait_for(self.stream_reader.readexactly(4), timeout=5.0)
        except asyncio.TimeoutError:
            print("Timeout occurred while reading size.")
            return None
        data_size = int.from_bytes(size_buffer, 'big')
        print(f"data_size :{data_size}")
        data_buffer = await self.stream_reader.readexactly(data_size)
        print(f"data_buffer :{data_buffer}")
        data = msgpack.unpackb(data_buffer, raw=False)
        return model_type(**data)

    async def write_async(self, obj: any):
        """Serialize and write a model to the stream."""
        data = msgpack.packb(obj, use_bin_type=True)
        data_len = len(data).to_bytes(4, 'big')
        self.stream_writer.write(data_len + data)
        await self.stream_writer.drain()

    async def initial_async(self):
        """Initialize the processor."""
        loop = asyncio.get_event_loop()
        self.task = loop.create_task(self._close_on_signal())

    async def _close_on_signal(self):
        """Listen for the close signal."""
        while not self.close_token.is_set():
            line = await asyncio.get_event_loop().run_in_executor(None, input)
            if line.strip() == MessageCommunicate.CLOSED_SIGNAL:
                self.close_token.set()

    async def do_work_async(self, process: Callable[[MessageInputTask], MessageOutputTask]):
        """Process tasks asynchronously."""
        print("Worker starting...")
        print("Enter text 'quit' to stop:")
        while not self.close_token.is_set():
            try:
                print("raeding data...")
                task = await self.read_async(MessageInputTask)
                result = process(task)
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
    return MessageOutputTask(
        message=f"Processed: {input_task.message}",
        status=MessageStatus.MessageDone,
        headers=input_task.headers,
        reply_queue_name=None
    )


async def main():
    pipe_name = input("Enter pipe name: ")
    pipe_name = f"/tmp/CoreFxPipe_{pipe_name}"
    reader, writer = await asyncio.open_unix_connection(pipe_name)
    processor = MessageProcessor((reader, writer))
    await processor.initial_async()
    await processor.do_work_async(example_process)


# Run the example if this is the main module
if __name__ == "__main__":
    asyncio.run(main())
