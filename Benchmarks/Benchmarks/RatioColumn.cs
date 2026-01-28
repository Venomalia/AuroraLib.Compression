using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.IO;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System.IO.Compression;

namespace Benchmarks.Benchmarks
{
    public class SpeedColumn : IColumn
    {
        public string Id => "MB/s";
        public string ColumnName => "MB/s";
        public string Legend => "Throughput in MB per second";

        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 1;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            int kb = (int)benchmarkCase.Parameters["Kb"];
            double mb = kb / 1000.0;

            var report = summary.Reports.FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
            if (report?.ResultStatistics == null)
                return "-";

            double seconds = report.ResultStatistics.Mean / 1_000_000_000.0; // Mean is ns!
            double mbPerSec = mb / seconds;

            return mbPerSec.ToString("F2", style.CultureInfo);
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
            => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    }

    public class RatioColumn : IColumn
    {
        public string Id => "Ratio";
        public string ColumnName => "Ratio %";
        public string Legend => "Compression ratio in percent";

        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
            => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var algObj = benchmarkCase.Parameters["Algorithm"];
            if (algObj is not Type algType)
                return "?";
            var instance = (ICompressionAlgorithm)Activator.CreateInstance(algType)!;

            int kb = (int)benchmarkCase.Parameters["Kb"];
            CompressionLevel level = (CompressionLevel)benchmarkCase.Parameters["Level"];
            const string filePath = "Test.bmp";
            if (!File.Exists(filePath))
                return "file?";

            using var raw = new SubStream(File.OpenRead(filePath),1024*kb);
            using var output = new MemoryStream();

            instance.Compress(raw, output, level);

            double ratio = (double)output.Length / raw.Length * 100.0;
            return ratio.ToString("F2", style.CultureInfo) + " %";
        }

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    }
}
