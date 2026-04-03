using AuroraLib.Core.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuroraLib.Compression.CLI
{
    internal static class ArgumentParser
    {
        public enum Modes
        {
            Help,
            Compress,
            Decompress,
            Mime,
            BruteForce
        }

        public enum Flags
        {
            In,
            OUt,
            Algo,
            Level,
            LegacyMode,
            Endian,
            Overwrite,
            Quiet,
            SCan,
            Size,
            OFfset,
            MaxWindow,
            WRam
        }

        private static readonly Dictionary<string, Modes> _modeMap;
        private static readonly Dictionary<string, Flags> _flagMap;

        static ArgumentParser()
        {
            _modeMap = new Dictionary<string, Modes>(StringComparer.OrdinalIgnoreCase);
            _flagMap = new Dictionary<string, Flags>(StringComparer.OrdinalIgnoreCase);

#if NETFRAMEWORK

            foreach (var flagob in Enum.GetValues(typeof(Modes)))
            {
                Modes flag = (Modes)flagob;
#else
            foreach (var flag in Enum.GetValues<Modes>())
            {
#endif
                string name = flag.ToString();

                _modeMap.Add("-" + name.ToLower(), flag);
                string shortName = "-" + new string(name.Where(char.IsUpper).ToArray()).ToLower();
                if (!_modeMap.TryAdd(shortName, flag))

                    throw new Exception($"Key '{shortName}' is Already in use");
            }

#if NETFRAMEWORK

            foreach (var flagob in Enum.GetValues(typeof(Flags)))
            {
                Flags flag = (Flags)flagob;
#else
            foreach (var flag in Enum.GetValues<Flags>())
            {
#endif
                string name = flag.ToString();

                _flagMap.Add("-" + name.ToLower(), flag);
                string shortName = "-" + new string(name.Where(char.IsUpper).ToArray()).ToLower();
                if (!_flagMap.TryAdd(shortName, flag))

                    throw new Exception($"Key '{shortName}' is Already in use");
            }
        }

        public static Modes Parse(string[] args, out Dictionary<Flags, string?> argsDict)
        {
            argsDict = new Dictionary<Flags, string?>();

            // parse Modes
            if (args.Length == 0)
                return Modes.Help;

            if (!_modeMap.TryGetValue(args[0], out var mode))
            {
                // Quick mode: single non-flag argument (file path)
#if NETFRAMEWORK
                if (args.Length == 1 && !args[0].StartsWith("-"))
#else
                if (args.Length == 1 && !args[0].StartsWith('-'))
#endif
                {
                    argsDict[Flags.In] = args[0];
                    argsDict[Flags.Overwrite] = null;
                    return Modes.Decompress;
                }

                throw new ArgumentException($"Unknown mode: {args[0]}. Use -help for usage");
            }

            // parse Flags
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("-"))
                    continue;

                if (!_flagMap.TryGetValue(arg, out var flag))
                    throw new ArgumentException($"Unknown flag: {arg}. Use -help for usage");

                if (argsDict.ContainsKey(flag))
                    throw new ArgumentException($"Duplicate flag: {arg}.");

                string? value = null;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    value = args[++i];
                }

                argsDict[flag] = value;
            }

            return mode;
        }
    }
}
