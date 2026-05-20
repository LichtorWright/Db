using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Dbuild
{
    /// <summary>
    /// Watches a folder for foreign-language source files and automatically generates
    /// corresponding Db stub files (.stub) via a pluggable <see cref="IStubConverter"/>.
    /// Add support for a new target language by implementing <see cref="IStubConverter"/>
    /// and passing an instance here.
    /// </summary>
    public class ForeignFolderWatcher : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly IStubConverter _converter;
        private readonly string _sourceFolder;
        private readonly string _outputFolder;
        private bool _disposed;

        /// <param name="converter">Language-specific converter (e.g. <see cref="CppHeaderToDbStub"/>).</param>
        /// <param name="sourceFolder">Folder containing the foreign source files to watch.</param>
        /// <param name="outputFolder">Folder where generated <c>.stub</c> files are written.</param>
        public ForeignFolderWatcher(IStubConverter converter, string sourceFolder, string outputFolder)
        {
            _converter    = converter;
            _sourceFolder = sourceFolder;
            _outputFolder = outputFolder;

            Directory.CreateDirectory(_sourceFolder);
            Directory.CreateDirectory(_outputFolder);
        }

        /// <summary>Starts watching the source folder for all extensions the converter handles.</summary>
        public void Start()
        {
            foreach (string ext in _converter.WatchExtensions)
            {
                var w = new FileSystemWatcher(_sourceFolder)
                {
                    NotifyFilter         = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter               = ext,
                    EnableRaisingEvents  = false
                };

                w.Created += OnFileCreated;
                w.Changed += OnFileChanged;
                w.Deleted += OnFileDeleted;

                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }

            Console.WriteLine($"[ForeignWatcher] Watching: {_sourceFolder}  ({string.Join(", ", _converter.WatchExtensions)})");
            Console.WriteLine($"[ForeignWatcher] Output:   {_outputFolder}");

            ProcessExistingFiles();
        }

        private void ProcessExistingFiles()
        {
            foreach (string ext in _converter.WatchExtensions)
            {
                foreach (string file in Directory.GetFiles(_sourceFolder, ext))
                    ConvertFile(file);
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[ForeignWatcher] New: {e.Name}");
            ConvertFile(e.FullPath);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[ForeignWatcher] Changed: {e.Name}");
            Thread.Sleep(100); // debounce
            ConvertFile(e.FullPath);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[ForeignWatcher] Deleted: {e.Name}");
            string stub = StubPathFor(e.FullPath);
            if (File.Exists(stub))
            {
                File.Delete(stub);
                Console.WriteLine($"[ForeignWatcher] Removed stub: {Path.GetFileName(stub)}");
            }
        }

        private void ConvertFile(string sourcePath)
        {
            try
            {
                string stub = StubPathFor(sourcePath);
                _converter.Convert(sourcePath, stub);
                Console.WriteLine($"[ForeignWatcher] Generated: {Path.GetFileName(stub)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ForeignWatcher] Error ({Path.GetFileName(sourcePath)}): {ex.Message}");
            }
        }

        private string StubPathFor(string sourcePath)
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            return Path.Combine(_outputFolder, $"{name}.stub");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    foreach (var w in _watchers)
                        w.Dispose();

                _disposed = true;
            }
        }
    }
}