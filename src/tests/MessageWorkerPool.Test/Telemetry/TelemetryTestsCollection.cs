using Xunit;

namespace MessageWorkerPool.Test.Telemetry
{
    /// <summary>
    /// Collection definition to ensure telemetry tests run serially
    /// since they share the static TelemetryManager.
    /// </summary>
    [CollectionDefinition("TelemetryTests", DisableParallelization = true)]
    public class TelemetryTestsCollection
    {
    }
}
