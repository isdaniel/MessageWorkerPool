using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageWorkerPool.Extensions
{
    internal static class HelperExtension
    {
        internal static IDictionary<string, string> ConvertToStringMap(this IDictionary<string,object> source)
        {
            return source != null ? source.ToDictionary(x => x.Key, x => x.Value?.ToString())
                   : new Dictionary<string, string>();
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
