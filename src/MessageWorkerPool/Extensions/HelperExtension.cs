using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageWorkerPool.Extensions
{
    internal static class HelperExtension
    {
        internal static IDictionary<string, string> ConvertToString(this IDictionary<string,object> source)
        {
            return source != null ? source.ToDictionary(x => x.Key, x => x.Value?.ToString())
                   : new Dictionary<string, string>();
        }

        internal static IDictionary<string, object> ConvertToObject(this IDictionary<string, string> source)
        {
            return source != null ? source.ToDictionary(x => x.Key, x => (object)x.Value)
                   : new Dictionary<string, object>();
        }
    }
}
