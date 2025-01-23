using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<NamedPipeVsStandardInputBenchmark>(new Config());
        //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
        Console.Read();
    }
}
