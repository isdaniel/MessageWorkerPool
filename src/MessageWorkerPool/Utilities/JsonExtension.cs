// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace MessageWorkerPool.Utilities
{
    internal static class JsonHelper
    {
        public static bool TryParse<T>(this string json, out T result)
        {
            try
            {
                result = JsonSerializer.Deserialize<T>(json);
                return true;
            }
            catch (JsonException)
            {
                result = default;
                return false;
            }
        }
    }

}
