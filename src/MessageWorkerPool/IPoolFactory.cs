using System.Collections.Generic;

namespace MessageWorkerPool
{
    /// <summary>
    /// WorkPool Pool factory that is responsible for creating process/threads pool map
    /// </summary>
    public interface IPoolFactory
    {
        Dictionary<string, IWorkerPool> GetPools(PoolSetting[] setting);
    }
}
