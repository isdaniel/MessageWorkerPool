using System;
using Newtonsoft.Json;
using MessageWorkerPool.Utilities;

namespace WorkerClient
{
    public static class JsonExtension
    {
        public static string ToIgnoreMessage(this string message) 
        {
            return JsonConvert.SerializeObject(new MessageOutputTask()
            {
                Message = message,
                Status = MessageStatus.IGNORE_MESSAGE
            });
        }

        public static string ToJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T ToObject<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}