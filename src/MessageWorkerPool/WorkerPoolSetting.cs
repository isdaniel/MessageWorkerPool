namespace MessageWorkerPool
{
    /// <summary>
    /// Pool setting
    /// </summary>
    public class WorkerPoolSetting
    {
        /// <summary>
        /// worker unit count
        /// </summary>
        public ushort WorkerUnitCount { get; set; }

        /// <summary>
        /// execute cli or commnad line
        /// </summary>
        public string CommnadLine { get; set; }
        /// <summary>
        /// parameter of cli or commnad line
        /// </summary>
        public string Arguments { get; set; }

    }
}
