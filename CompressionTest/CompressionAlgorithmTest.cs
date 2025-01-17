using AuroraLib.Compression;
using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using HashDepot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace CompressionTest
{
    [TestClass]
    public class CompressionAlgorithmTest
    {
        static CompressionAlgorithmTest()
        {
            LZ4.HashAlgorithm = b => XXHash.Hash32(b);
        }

        [TestMethod]
        public void LzssStaticDecodingTest()
        {
            using (FileStream compressData = new FileStream("Test.lz", FileMode.Open, FileAccess.Read))
            {
                LZSS lz = new LZSS(new LzProperties((byte)10, 6, 2));
                using (MemoryPoolStream decompressData = lz.Decompress(compressData))
                {
                    ulong decompressDataHash = XXHash.Hash64(decompressData.UnsafeAsSpan());
                    Assert.AreEqual(11520079745250749767, decompressDataHash);
                }
            }
        }

        public static IEnumerable<object[]> GetAvailableAlgorithms()
        {
            IEnumerable<Type> availableAlgorithmTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes().Where(s => typeof(ICompressionAlgorithm).IsAssignableFrom(s) && !s.IsInterface && !s.IsAbstract));
            return availableAlgorithmTypes.Select(x => new object[] { (ICompressionAlgorithm)Activator.CreateInstance(x)! });
        }

        private static FormatDictionary Formats = new FormatDictionary(AppDomain.CurrentDomain.GetAssemblies());

        [TestMethod]
        [DynamicData(nameof(GetAvailableAlgorithms), DynamicDataSourceType.Method)]
        public void DataRecognitionTest(ICompressionAlgorithm algorithm)
        {
            using (FileStream testData = new FileStream("Test.bmp", FileMode.Open, FileAccess.Read))
            using (MemoryPoolStream compressData = algorithm.Compress(testData, CompressionLevel.NoCompression))
            {
                ReadOnlySpan<char> fileNameAndExtension = $"Test.bmp.{algorithm.GetType().Name}".AsSpan();
                if (Formats.Identify(compressData, fileNameAndExtension, out IFormatInfo format) && format.Class != null && typeof(ICompressionAlgorithm).IsAssignableFrom(format.Class))
                {
                    Assert.AreEqual(format.Class, algorithm.GetType());
                }
                else
                {
                    Assert.Fail();
                }
            }
        }



        [TestMethod]
        [DynamicData(nameof(GetAvailableAlgorithms), DynamicDataSourceType.Method)]
        public void EncodingAndDecodingMatchTest(ICompressionAlgorithm algorithm)
        {
            using (FileStream testData = new FileStream("Test.bmp", FileMode.Open, FileAccess.Read))
            using (SpanBuffer<byte> testDataBytes = new SpanBuffer<byte>((int)testData.Length))
            {
                testData.Read(testDataBytes);
                ulong expectedHash = XXHash.Hash64(testDataBytes);

                using (Stream compressData = algorithm.Compress(testDataBytes))
                using (MemoryPoolStream decompressData = algorithm.Decompress(compressData))
                {
                    ulong decompressDataHash = XXHash.Hash64(decompressData.UnsafeAsSpan());
                    Assert.AreEqual(expectedHash, decompressDataHash);
                }
            }
        }

        [TestMethod]
        public void EncodingAndDecodingMatchTest_LZ4Frame()
        {
            LZ4 LZ4Frame = new LZ4() { FrameType = LZ4.FrameTypes.LZ4FrameHeader, Flags = LZ4.FrameDescriptorFlags.IsVersion1 | LZ4.FrameDescriptorFlags.HasContentSize | LZ4.FrameDescriptorFlags.HasContentChecksum | LZ4.FrameDescriptorFlags.HasBlockChecksum};
            EncodingAndDecodingMatchTest(LZ4Frame);
        }
    }
}
