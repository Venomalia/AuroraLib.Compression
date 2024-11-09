using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.IO;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class TestAlgorithm<T> where T : ICompressionAlgorithm, new()
    {
        public const string TestFile = "Test.bmp";
        public MemoryPoolStream TestRawData = (MemoryPoolStream)Stream.Null;
        public MemoryPoolStream TestComData = (MemoryPoolStream)Stream.Null;
        public T Instance = new();

        [Params(1, 10)]
        public int MB;

        [GlobalSetup]
        public void GlobalSetup()
        {
            using FileStream input = new(TestFile, FileMode.Open, FileAccess.Read);
            TestRawData = new MemoryPoolStream(input, 1024 * 1024); //read 1mb
            TestComData = Instance.Compress(TestRawData);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TestRawData.Dispose();
            TestComData.Dispose();
        }

        [Benchmark]
        public void Compress()
        {
            using MemoryPoolStream output = new();
            for (int i = 0; i < MB; i++)
            {
                TestRawData.Position = 0;
                output.Position = 0;
                Instance.Compress(TestRawData, output);
            }
        }

        [Benchmark]
        public void Decompress()
        {
            using MemoryPoolStream output = new();
            for (int i = 0; i < MB; i++)
            {
                TestComData.Position = 0;
                output.Position = 0;
                Instance.Decompress(TestComData, output);
            }
        }
    }
}
