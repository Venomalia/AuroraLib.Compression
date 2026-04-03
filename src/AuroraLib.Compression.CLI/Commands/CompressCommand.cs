using AuroraLib.Compression.Formats.Nintendo;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;
using static AuroraLib.Compression.CLI.ArgumentParser;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class CompressCommand
    {
        public static bool Execute(string sourceFile, string destinationFile, IFormatInfo format, CompressionSettings settings = default, Endian? order = null, bool useWram = false)
        {
            HelpPrinter.PrintOperation(nameof(Modes.Compress), sourceFile, destinationFile);
            HelpPrinter.PrintFormatInfo(format);

            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var encoder = format.CreateInstance();
            if (encoder is ICompressionEncoder compressionEncoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);

                if (order != null && encoder is IEndianDependentFormat encoderEndianSettings)
                {
                    encoderEndianSettings.FormatByteOrder = order.Value;
                }

                if (encoder is IGbaRamMode gbaRamMode)
                {
                    gbaRamMode.GbaVramCompatibilityMode = !useWram;
                }

                HelpPrinter.PrintCompressionSettings(settings, compressionEncoder);

                DateTime startTime = DateTime.Now;
                HelpPrinter.PrintOperationStart(startTime);
                compressionEncoder.Compress(source, destination, settings);
                HelpPrinter.PrintOperationEnd(startTime);
                HelpPrinter.PrintStats(source.Length, destination.Length);
                return true;
            }
            return false; // no encode
        }
    }
}
