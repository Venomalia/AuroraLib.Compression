using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class CompressCommand
    {
        public static bool Execute(string sourceFile, string destinationFile, IFormatInfo format, CompressionSettings settings = default, Endian? order = null)
        {
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var encoder = format.CreateInstance();
            if (encoder is ICompressionEncoder compressionEncoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);

                if (order != null && encoder is IEndianDependentFormat encoderEndianSettings)
                {
                    encoderEndianSettings.FormatByteOrder = order.Value;
                }

                compressionEncoder.Compress(source, destination, settings);
                Console.WriteLine($"{source.Length / (1024.0 * 1024):F2} MB input, {destination.Length / (1024.0 * 1024):F2} MB output, compression ratio: {(100.0 * destination.Length / source.Length):F2}%");

                return true;
            }
            return false; // no encode
        }
    }
}
