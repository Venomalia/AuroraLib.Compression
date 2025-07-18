using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AuroraLib.Compression.CLI
{

    [DebuggerDisplay("{FullName} ({MIMEType})")]
    public sealed class SpezialFormatInfo<T> : IFormatInfo, IFormatRecognition, IEquatable<IFormatInfo>
    {
        public string FullName { get; }

        public MediaType MIMEType { get; }

        public IEnumerable<string> FileExtensions { get; }

        public IIdentifier? Identifier => null;

        public int IdentifierOffset => 0;

        public Type? Class => typeof(T);

        private Func<T> CreateInstanceFunc { get; }

        public SpezialFormatInfo(string fullName, MediaType mediaType, string fileExtension, Func<T> createInstanceFunc) : this(fullName, mediaType, new string[1] { fileExtension }, createInstanceFunc)
        { }

        public SpezialFormatInfo(string fullName, MediaType mediaType, IEnumerable<string> fileExtensions, Func<T> createInstanceFunc)
        {
            ThrowIf.Null(fullName, "fullName");
            ThrowIf.Null(mediaType, "mediaType");
            ThrowIf.Null(fileExtensions, "fileExtensions");
            FullName = fullName;
            MIMEType = mediaType;
            FileExtensions = fileExtensions;
            CreateInstanceFunc = createInstanceFunc;

        }

        public object? CreateInstance() => CreateInstanceFunc();

        public bool Equals(IFormatInfo? other)
        {
            return other?.MIMEType.Equals(MIMEType) ?? false;
        }

        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default) => false;
    }
}
