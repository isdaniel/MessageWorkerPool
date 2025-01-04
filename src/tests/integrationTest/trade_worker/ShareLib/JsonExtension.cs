using System.Text.Json;
using MessageWorkerPool.Utilities;

namespace ShareLib
{
    public static class JsonExtension {
        public static string ToIgnoreMessage(this string message) {
            return JsonSerializer.Serialize(new MessageOutputTask()
            {
                Message = message,
                Status = MessageStatus.IGNORE_MESSAGE
            });
        }

        public static string ToJson(this MessageOutputTask task)
        {
            return JsonSerializer.Serialize(task);
        }

        public static MessageInputTask ToMessageInputTask(this string message)
        {
            return JsonSerializer.Deserialize<MessageInputTask>(message);
        }
    }
}

