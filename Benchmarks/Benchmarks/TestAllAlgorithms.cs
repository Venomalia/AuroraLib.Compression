using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.IO;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class TestAllAlgorithms
    {
        public const string TestFile = "Test.bmp";
        public MemoryPoolStream TestRawData;
        public MemoryPoolStream TestComData;

        // Test all unique algorithm.
        [Params(typeof(ALLZ), typeof(CLZ0), typeof(CNS), typeof(CNX2), typeof(LZ00), typeof(LZ02), typeof(LZ10), typeof(LZ11), typeof(LZ40), typeof(LZO), typeof(LZShrek), typeof(LZSS), typeof(MIO0), typeof(PRS), typeof(RefPack), typeof(RLE30), typeof(Yay0), typeof(Yaz0))]
        public Type Algorithm;
        public ICompressionAlgorithm Instance;

        [Params(1, 10)]
        public int MB;

        [GlobalSetup]
        public void GlobalSetup()
        {
            Instance = (ICompressionAlgorithm)Activator.CreateInstance(Algorithm);
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
