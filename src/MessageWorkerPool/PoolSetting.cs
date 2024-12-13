namespace MessageWorkerPool
{
    /// <summary>
    /// Pool setting
    /// </summary>
    public class PoolSetting
    {
        /// <summary>
        /// worker unit count
        /// </summary>
        public ushort WorkUnitCount { get; set; }
        /// <summary>
        /// process group name
        /// </summary>
        public string Group { get; set; }
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
