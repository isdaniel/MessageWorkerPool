import json
import time
from typing import Callable, Optional

class MessageStatus:
    IGNORE_MESSAGE = -1
    MESSAGE_DONE = 200
    MESSAGE_DONE_WITH_REPLY = 201

class MessageOutputTask:
    def __init__(self, message: str, status: str):
        self.Message = message
        self.Status = status

    def to_json(self) -> str:
        return json.dumps(self.__dict__)

class MessageInputTask:
    @staticmethod
    def from_json(message: str) -> Optional['MessageInputTask']:
        try:
            data = json.loads(message)
            # Adjust the following as per the actual structure of MessageInputTask
            return MessageInputTask(**data)
        except json.JSONDecodeError:
            return None

    def __init__(self, **kwargs):
        for key, value in kwargs.items():
            setattr(self, key, value)

class JsonExtension:
    @staticmethod
    def to_ignore_message(message: str) -> str:
        task = MessageOutputTask(message, MessageStatus.IGNORE_MESSAGE)
        return task.to_json()

class MessageProcessor:
    def __init__(self):
        pass

    def do_work(self, process: Callable[[MessageInputTask], MessageOutputTask]):
        print(JsonExtension.to_ignore_message("worker starting..."))
        print(JsonExtension.to_ignore_message("Enter text 'quit' to stop:"))

        while True:
            try:
                input_text = input()
                if input_text.lower() == "quit":
                    print(JsonExtension.to_ignore_message("Exiting program."))
                    break

                task = MessageInputTask.from_json(input_text)
                if task is None:
                    # TODO: handle invalid task
                    pass
                else:
                    result = process(task)
                    print(result.to_json())

            except Exception as ex:
                print(f"Json Parse Error: {ex}", file=sys.stderr)

def main():
    processor = MessageProcessor()

    def process_function(task: MessageInputTask) -> MessageOutputTask:
        print(JsonExtension.to_ignore_message(f"this is func task.., message {task.Message}, Sleeping 1s"))
        time.sleep(1)
        return MessageOutputTask(
            message="New OutPut Message!",
            status=MessageStatus.MESSAGE_DONE
        )

    processor.do_work(process_function)

if __name__ == "__main__":
    main()
