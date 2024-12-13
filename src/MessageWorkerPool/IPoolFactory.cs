using System.Collections.Generic;

namespace MessageWorkerPool
{
    public interface IPoolFactory
    {
        Dictionary<string, IWorkerPool> GetPools(PoolSetting[] setting);
    }
}
