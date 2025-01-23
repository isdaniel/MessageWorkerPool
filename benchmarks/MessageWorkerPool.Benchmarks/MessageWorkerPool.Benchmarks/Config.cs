using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
public class Config : ManualConfig
{
    public const int Iterations = 5;

    public Config()
    {
        AddLogger(ConsoleLogger.Default);

        AddExporter(CsvExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);

        AddDiagnoser(MemoryDiagnoser.Default,ThreadingDiagnoser.Default);
        AddColumn(TargetMethodColumn.Method);
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Error);
        AddColumn(StatisticColumn.Iterations);
        AddColumn(StatisticColumn.P67);
        AddColumn(StatisticColumn.P90);
        AddColumn(StatisticColumn.P95);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumnProvider(DefaultColumnProviders.Metrics);        

        AddJob(Job.ShortRun
               .WithLaunchCount(1)
               .WithWarmupCount(2)
               .WithUnrollFactor(Iterations)
               .WithIterationCount(3)
        );
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
        Options |= ConfigOptions.JoinSummary;
    }
}
