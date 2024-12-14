using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageWorkerPool
{
    public class WorkerPoolFactory : IPoolFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public WorkerPoolFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Get Worker Pool collection by group.
        /// </summary>
        /// <param name="setting">Pool setting</param>
        /// <returns>Return a process pool by group condition</returns>
        /// <exception cref="ArgumentException"></exception>
        public Dictionary<string, IWorkerPool> GetPools(PoolSetting[] setting)
        {
            if (setting.Any(x => string.IsNullOrEmpty(x.CommnadLine)))
            {
                throw new ArgumentException("PoolType.Process need to declare FilePath!");
            }

            return setting.ToDictionary(x => x.Group, y => (IWorkerPool)new ProcessPool(y, _loggerFactory));
        }
    }
}
