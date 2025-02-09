using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.KafkaMq
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
        public KafkaMqWorkerPool(
            KafkaSetting<TKey> kafkaSetting,
            WorkerPoolSetting workerSetting,
            ILoggerFactory loggerFactory) : base(workerSetting, loggerFactory)
        {
            _kafkaSetting = kafkaSetting;
        }

        protected override IWorker GetWorker()
        {
            var logger = _loggerFactory.CreateLogger<KafkaMqWorker<TKey>>();
            return new KafkaMqWorker<TKey>(_workerSetting, _kafkaSetting, logger);
        }
    }
}
