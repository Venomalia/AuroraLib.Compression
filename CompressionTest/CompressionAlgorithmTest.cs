using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.IO;
using HashDepot;
using System.IO.Compression;

namespace CompressionTest
{
    [TestClass]
    public class CompressionAlgorithmTest
    {
        [TestMethod]
        public void LzssStaticDecodingTest()
        {
            using FileStream compressData = new("Test.lz", FileMode.Open, FileAccess.Read);
            LZSS lz = new(new((byte)10, 6, 2));

            using MemoryPoolStream decompressData = lz.Decompress(compressData);

            ulong decompressDataHash = XXHash.Hash64(decompressData.UnsaveAsSpan());
            Assert.AreEqual(11520079745250749767, decompressDataHash);
        }

        public static IEnumerable<object[]> GetAvailableAlgorithms()
        {
            IEnumerable<Type> availableAlgorithmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes().Where(s => typeof(ICompressionAlgorithm).IsAssignableFrom(s) && !s.IsInterface && !s.IsAbstract));
            return availableAlgorithmTypes.Select(x => new object[] { (ICompressionAlgorithm)Activator.CreateInstance(x) });
        }

        [TestMethod]
        [DynamicData(nameof(GetAvailableAlgorithms), DynamicDataSourceType.Method)]
        public void DataRecognitionTest(ICompressionAlgorithm algorithm)
        {
            using FileStream testData = new("Test.bmp", FileMode.Open, FileAccess.Read);

            using MemoryPoolStream compressData = algorithm.Compress(testData, CompressionLevel.NoCompression);

            Assert.IsTrue(algorithm.IsMatch(compressData, $".{algorithm.GetType().Name}"));
        }

        [TestMethod]
        [DynamicData(nameof(GetAvailableAlgorithms), DynamicDataSourceType.Method)]
        public void EncodingAndDecodingMatchTest(ICompressionAlgorithm algorithm)
        {
            using FileStream testData = new("Test.bmp", FileMode.Open, FileAccess.Read);
            using SpanBuffer<byte> testDataBytes = new((int)testData.Length);
            testData.Read(testDataBytes);
            ulong expectedHash = XXHash.Hash64(testDataBytes);

            using MemoryPoolStream compressData = algorithm.Compress(testDataBytes);
            using MemoryPoolStream decompressData = algorithm.Decompress(compressData);

            ulong decompressDataHash = XXHash.Hash64(decompressData.UnsaveAsSpan());
            Assert.AreEqual(expectedHash, decompressDataHash);
        }

    }
}