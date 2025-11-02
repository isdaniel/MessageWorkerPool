using Microsoft.Extensions.Logging;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool.Kafka
{
    public class KafkaMqWorkerPool<TKey> : WorkerPoolBase
    {
        private readonly KafkaSetting<TKey> _kafkaSetting;

        /// <summary>
        /// Create KafkaMqWorkerPool.
        /// </summary>
        /// <param name="kafkaSetting">Kafka Setting</param>
        /// <param name="workerSetting">Worker Pool Setting</param>
        /// <param name="loggerFactory"></param>
        /// <param name="telemetryManager">The telemetry manager for tracking pool operations.</param>
        public KafkaMqWorkerPool(
            KafkaSetting<TKey> kafkaSetting,
            WorkerPoolSetting workerSetting,
            ILoggerFactory loggerFactory,
            ITelemetryManager telemetryManager = null) : base(workerSetting, loggerFactory, telemetryManager)
        {
            _kafkaSetting = kafkaSetting;
        }

        protected override IWorker GetWorker()
        {
            var logger = _loggerFactory.CreateLogger<KafkaMqWorker<TKey>>();
            return new KafkaMqWorker<TKey>(_workerSetting, _kafkaSetting, logger, _telemetryManager);
        }
    }
}
