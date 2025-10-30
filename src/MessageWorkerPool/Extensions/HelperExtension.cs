using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageWorkerPool.Extensions
{
    internal static class HelperExtension
    {
        internal static IDictionary<string, string> ConvertToStringMap(this IDictionary<string,object> source)
        {
            if (source == null)
                return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            foreach (var kvp in source)
            {
                if (kvp.Value is byte[] bytes)
                {
                    // Convert byte array to UTF-8 string
                    result[kvp.Key] = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    result[kvp.Key] = kvp.Value?.ToString();
                }
            }
            return result;
        }

        internal static IDictionary<string, object> ConvertToObjectMap(this IDictionary<string, string> source)
        {
            return source != null ? source.ToDictionary(x => x.Key, x => (object)x.Value)
                   : new Dictionary<string, object>();
        }

        internal static TValue TryGetValueOrDefault<TValue>(this IDictionary<string, TValue> source,string key)
        {
            if (string.IsNullOrEmpty(key)) {
                throw new ArgumentNullException(nameof(key));
            }

            if (source.TryGetValue(key,out TValue res))
            {
                return res;
            }

            return default(TValue);
        }
    }
}
