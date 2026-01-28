using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.IO;
using BenchmarkDotNet.Attributes;
using System.IO.Compression;

namespace Benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class TestAllAlgorithms
    {
        public const string TestFile = "Test.bmp";
        public Stream TestRawData = Stream.Null;
        public Stream TestComData = Stream.Null;

        // Test all unique algorithm.
        [Params(typeof(ALLZ), typeof(CLZ0), typeof(CNS), typeof(CNX2), typeof(LZ00), typeof(LZ02), typeof(LZ10), typeof(LZ11), typeof(LZ40), typeof(LZO), typeof(LZShrek), typeof(LZSS), typeof(MIO0), typeof(PRS), typeof(RefPack), typeof(RLE30), typeof(Yay0), typeof(Yaz0), typeof(LZ4Legacy), typeof(HWGZ), typeof(HUF20), typeof(BLZ), typeof(CRILAYLA))]
        public Type Algorithm = null!;

        [Params(CompressionLevel.Optimal,CompressionLevel.Fastest)]
        public CompressionLevel Level;

        [Params(1000)]
        public int Kb;

        public ICompressionAlgorithm Instance = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            Instance = (ICompressionAlgorithm)Activator.CreateInstance(Algorithm)!;
            using FileStream input = new(TestFile, FileMode.Open, FileAccess.Read);
            TestRawData = new MemoryPoolStream(input, 1024 * Kb);
            TestComData = Instance.Compress(TestRawData, Level);
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
            TestRawData.Position = 0;
            output.Position = 0;
            Instance.Compress(TestRawData, output, Level);
        }

        [Benchmark]
        public void Decompress()
        {
            using MemoryPoolStream output = new();
            TestComData.Position = 0;
            output.Position = 0;
            Instance.Decompress(TestComData, output);
        }
    }
}
