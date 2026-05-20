using taste;

namespace Dbuild
{
    /// <summary>
    /// Converts foreign-language source files (e.g. C++ headers, Rust crates)
    /// into Db stub files (.stub) that feed the type system.
    /// </summary>
    public interface IStubConverter
    {
        /// <summary>
        /// File-filter patterns this converter handles, e.g. <c>*.h</c>, <c>*.hpp</c>.
        /// Each pattern is passed directly to <see cref="System.IO.FileSystemWatcher.Filter"/>.
        /// </summary>
        string[] WatchExtensions { get; }

        /// <summary>
        /// Converts a single source file and writes the resulting stub to
        /// <paramref name="outputPath"/>.
        /// </summary>
        /// <param name="inputPath">Absolute path to the foreign source file.</param>
        /// <param name="outputPath">Absolute path for the generated <c>.stub</c> file.</param>
        void Convert(string inputPath, string outputPath);

        /// <summary>
        /// Reads a foreign source file and registers all exported types and members
        /// directly into <paramref name="registry"/>, without writing any files to disk.
        /// </summary>
        /// <param name="inputPath">Absolute path to the foreign source file.</param>
        /// <param name="registry">Registry to populate with stub entries.</param>
        void ImportTypes(string inputPath, StubRegistry registry);
    }
}
