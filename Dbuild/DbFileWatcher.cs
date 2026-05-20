using System;
using System.IO;
using System.Threading;
using taste;

namespace Dbuild
{
    /// <summary>
    /// Watches for .db files and automatically generates corresponding .g.cpp files.
    /// The .g suffix indicates "generated" C++ code.
    /// </summary>
    public class DbFileWatcher : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private readonly string _watchFolder;
        private readonly string _outputFolder;
        private readonly StubRegistry? _registry;
        private bool _disposed;
        
        
        /// <summary>
        /// Initializes a new instance of the <see cref="DbFileWatcher"/> class.
        /// </summary>
        /// <param name="watchFolder">Path to the folder containing .db files</param>
        /// <param name="outputFolder">Path where .g.cpp files will be generated</param>
        /// <param name="registry">Pre-loaded stub registry for type resolution. Pass null to skip stub resolution.</param>
        public DbFileWatcher(string watchFolder, string outputFolder, StubRegistry? registry = null)
        {
            _watchFolder = watchFolder;
            _outputFolder = outputFolder;
            _registry = registry;
            if (!Directory.Exists(_watchFolder))
            {
                Directory.CreateDirectory(_watchFolder);
            }
            
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }
        }
        
        /// <summary>
        /// Starts watching for .db file changes and auto-generates .g.cpp files.
        /// </summary>
        public void Start()
        {
            _watcher = new FileSystemWatcher(_watchFolder);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Filter = "*.db";
            
            _watcher.Created += OnDbFileCreated;
            _watcher.Changed += OnDbFileChanged;
            _watcher.Deleted += OnDbFileDeleted;
            
            _watcher.EnableRaisingEvents = true;
            
            Console.WriteLine($"[DbWatcher] Watching for .db files in: {_watchFolder}");
            Console.WriteLine($"[DbWatcher] Generating .g.cpp files in: {_outputFolder}");
            
            // Process existing files
            ProcessExistingFiles();
        }
        
        /// <summary>
        /// Processes all existing .db files in the watch folder.
        /// </summary>
        private void ProcessExistingFiles()
        {
            string[] files = Directory.GetFiles(_watchFolder, "*.db");
            foreach (string file in files)
            {
                // Skip stub files (they're already generated from headers)
                if (file.Contains("Stubs"))
                {
                    continue;
                }
                
                GenerateCppFile(file);
            }
        }
        
        /// <summary>
        /// Handles the Created event for .db files.
        /// </summary>
        private void OnDbFileCreated(object sender, FileSystemEventArgs e)
        {
            // Skip stub files
            if (e.FullPath.Contains("Stubs"))
            {
                return;
            }
            
            Console.WriteLine($"[DbWatcher] New .db file detected: {e.Name}");
            GenerateCppFile(e.FullPath);
        }
        
        /// <summary>
        /// Handles the Changed event for .db files (auto-regenerates on save).
        /// </summary>
        private void OnDbFileChanged(object sender, FileSystemEventArgs e)
        {
            // Skip stub files
            if (e.FullPath.Contains("Stubs"))
            {
                return;
            }
            
            Console.WriteLine($"[DbWatcher] .db file changed: {e.Name}");
            
            // Debounce rapid saves (common in editors)
            Thread.Sleep(200);
            GenerateCppFile(e.FullPath);
        }
        
        /// <summary>
        /// Handles the Deleted event for .db files.
        /// </summary>
        private void OnDbFileDeleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[DbWatcher] .db file deleted: {e.Name}");
            
            // Delete corresponding .g.cpp file
            string cppFile = GetCppFilePath(e.FullPath);
            if (File.Exists(cppFile))
            {
                File.Delete(cppFile);
                Console.WriteLine($"[DbWatcher] Deleted generated: {Path.GetFileName(cppFile)}");
            }
        }
        
        /// <summary>
        /// Transpiles a .db file to a .g.cpp file.
        /// </summary>
        /// <param name="dbFile">Full path to the .db file</param>
        private void GenerateCppFile(string dbFile)
        {
            try
            {
                string cppFile = GetCppFilePath(dbFile);
                string cppOutput = Path.Combine(_outputFolder, Path.GetFileName(cppFile));
                
                // Run transpilation via taste
                Transpiler.TranspileFile(dbFile, cppOutput, _registry);
                
                Console.WriteLine($"[DbWatcher] ✓ Generated: {Path.GetFileName(cppOutput)}");
            }
            catch (TranspilerException tex)
            {
                Console.WriteLine($"[DbWatcher] ✗ [E{tex.ErrorCode}] in {Path.GetFileName(dbFile)}: {tex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbWatcher] ✗ Error converting {Path.GetFileName(dbFile)}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the output .g.cpp file path for a given .db file.
        /// </summary>
        /// <param name="dbFile">Full path to the .db file</param>
        /// <returns>Full path to the .g.cpp file</returns>
        private string GetCppFilePath(string dbFile)
        {
            string dbName = Path.GetFileNameWithoutExtension(dbFile);
            return $"{dbName}.g.cpp";
        }
        
        /// <summary>
        /// Releases unmanaged resources used by the watcher.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _watcher?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}