using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Dbuild
{
    /// <summary>
    /// Manages C++ build process using CMake and native compilers.
    /// Handles compilation, linking, and error reporting for Db-generated C++ code.
    /// </summary>
    public class CppBuildSystem
    {
        private readonly string _workspaceRoot;
        private readonly string _projectName;
        private readonly string _buildFolder;
        private readonly string _cmakeExecutable;
        private readonly string _compilerExecutable;
        private readonly string _ninjaExecutable;
        private readonly string _vcpkgRoot;
        private readonly string _vcpkgToolchain;
        
        /// <summary>
        /// Gets or sets the build configuration (Debug/Release).
        /// </summary>
        public string Configuration { get; set; } = "Debug";
        
        /// <summary>
        /// Gets or sets whether to show detailed output.
        /// </summary>
        public bool Verbose { get; set; } = false;
        
        /// <summary>
        /// Event raised when build output is produced.
        /// </summary>
        public event EventHandler<BuildOutputEventArgs>? BuildOutput;
        
        /// <summary>
        /// Event raised when build errors are detected.
        /// </summary>
        public event EventHandler<BuildErrorEventArgs>? BuildError;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CppBuildSystem"/> class.
        /// </summary>
        /// <param name="workspaceRoot">Root folder of the Db project</param>
        /// <param name="projectName">Name of the project (matches .dbproj filename and project folder)</param>
        public CppBuildSystem(string workspaceRoot, string projectName)
        {
            _workspaceRoot = workspaceRoot;
            _projectName = projectName;
            _buildFolder = Path.Combine(workspaceRoot, projectName, "Build");
            
            _cmakeExecutable = FindCMake();
            _compilerExecutable = FindCompiler();
            _ninjaExecutable = FindNinja();
            _vcpkgRoot = FindVcpkg();
            _vcpkgToolchain = string.IsNullOrEmpty(_vcpkgRoot)
                ? string.Empty
                : Path.Combine(_vcpkgRoot, "scripts", "buildsystems", "vcpkg.cmake");
        }

        /// <summary>Runs vcvarsall.bat x64 and returns the resulting environment variables.</summary>
        private IReadOnlyDictionary<string, string>? GetMsvcEnvironment()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

            var vcvarsall = FindVcvarsall();
            if (string.IsNullOrEmpty(vcvarsall)) return null;

            // Run: cmd /C "vcvarsall.bat x64 & set"
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // Must use Arguments string (not ArgumentList) — cmd.exe parses its own command line
                // and ArgumentList wraps with \"...\" escaping that cmd.exe doesn't understand
                Arguments = $"/C call \"{vcvarsall}\" x64 & set",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                    env[key] = value;
            }

            return env.Count > 0 ? env : null;
        }

        private string FindVcvarsall()
        {
            // Standard VS 2022 Community / Professional / Enterprise paths
            var editions = new[] { "Community", "Professional", "Enterprise", "BuildTools" };
            foreach (var ed in editions)
            {
                var path = $@"C:\Program Files\Microsoft Visual Studio\2022\{ed}\VC\Auxiliary\Build\vcvarsall.bat";
                if (File.Exists(path)) return path;
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Configures the CMake build system.
        /// </summary>
        /// <returns>True if configuration succeeded, false otherwise</returns>
        public bool Configure()
        {
            OnBuildOutput($"Configuring CMake in {_buildFolder}...");
            
            if (!Directory.Exists(_buildFolder))
            {
                Directory.CreateDirectory(_buildFolder);
            }
            
            // Build argument list -- use ArgumentList so spaces inside values are never split by the shell
            var args = new List<string> { "-S", _workspaceRoot, "-B", _buildFolder };

            if (!string.IsNullOrEmpty(_ninjaExecutable))
            {
                // Prefer Ninja: single-config, fast, no VS project files needed
                args.Add("-G"); args.Add("Ninja");
                args.Add($"-DCMAKE_BUILD_TYPE={Configuration}");
                args.Add($"-DCMAKE_MAKE_PROGRAM={_ninjaExecutable}");

                // cl.exe is not on PATH outside a Developer Prompt — pass it explicitly
                if (!string.IsNullOrEmpty(_compilerExecutable) && File.Exists(_compilerExecutable))
                {
                    OnBuildOutput($"Using compiler: {_compilerExecutable}");
                    args.Add($"-DCMAKE_CXX_COMPILER={_compilerExecutable}");
                    args.Add($"-DCMAKE_C_COMPILER={Path.Combine(Path.GetDirectoryName(_compilerExecutable)!, "cl.exe")}");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Fall back to Visual Studio multi-config generator
                args.Add("-G"); args.Add("Visual Studio 17 2022");
                args.Add("-A"); args.Add("x64");
            }
            else
            {
                args.Add($"-DCMAKE_BUILD_TYPE={Configuration}");
            }

            // Integrate vcpkg toolchain if available
            if (!string.IsNullOrEmpty(_vcpkgToolchain) && File.Exists(_vcpkgToolchain))
            {
                OnBuildOutput($"Using vcpkg toolchain: {_vcpkgToolchain}");
                args.Add($"-DCMAKE_TOOLCHAIN_FILE={_vcpkgToolchain}");
            }

            var result = RunCommand(_cmakeExecutable, args, extraEnv: GetMsvcEnvironment());
            
            if (result.ExitCode == 0)
            {
                OnBuildOutput("✓ CMake configuration successful");
                return true;
            }
            else
            {
                OnBuildError("CMake configuration failed", result.Output, result.ExitCode);
                return false;
            }
        }
        
        /// <summary>
        /// Builds the C++ project.
        /// </summary>
        /// <returns>True if build succeeded, false otherwise</returns>
        public bool Build()
        {
            OnBuildOutput($"Building {Configuration} configuration...");
            
            var args = new List<string>
            {
                "--build", _buildFolder,
                "--config", Configuration
            };
            
            if (Verbose)
            {
                args.Add("--verbose");
            }
            
            var result = RunCommand(_cmakeExecutable, args, extraEnv: GetMsvcEnvironment());

            if (result.ExitCode == 0)
            {
                OnBuildOutput("✓ Build successful");
                return true;
            }
            else
            {
                ParseBuildErrors(result.Output);
                OnBuildError("Build failed", result.Output, result.ExitCode);
                return false;
            }
        }
        
        /// <summary>
        /// Configures and builds in one step.
        /// </summary>
        /// <returns>True if both steps succeeded, false otherwise</returns>
        public bool ConfigureAndBuild()
        {
            if (!Configure())
            {
                return false;
            }
            
            return Build();
        }
        
        /// <summary>
        /// Runs the built executable with debugging.
        /// </summary>
        /// <returns>Exit code of the executable</returns>
        public int RunWithDebug()
        {
            string exePath = FindBuiltExecutable();
            
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                OnBuildError("Executable not found", $"Could not find built executable in {_buildFolder}", -1);
                return -1;
            }
            
            OnBuildOutput($"Running {exePath}...");
            
            var result = RunCommand(exePath, Array.Empty<string>(), waitForExit: false);
            return result.ExitCode;
        }
        
        /// <summary>
        /// Checks for missing prerequisites.
        /// </summary>
        /// <returns>List of missing components</returns>
        public List<string> CheckPrerequisites()
        {
            var missing = new List<string>();
            
            if (string.IsNullOrEmpty(_cmakeExecutable) || !File.Exists(_cmakeExecutable))
            {
                missing.Add("CMake - Install from https://cmake.org/download/");
            }

            if (string.IsNullOrEmpty(_ninjaExecutable))
            {
                missing.Add("Ninja build tool - not found (checked PATH and vcpkg downloads). Install from https://ninja-build.org/ or via vcpkg.");
            }

            if (string.IsNullOrEmpty(_vcpkgRoot))
            {
                missing.Add("vcpkg - not found. Clone from https://github.com/microsoft/vcpkg");
            }
            
            if (string.IsNullOrEmpty(_compilerExecutable))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    missing.Add("Visual Studio 2022 with C++ workload");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    missing.Add("GCC or Clang (sudo apt install build-essential)");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    missing.Add("Xcode Command Line Tools (xcode-select --install)");
                }
            }
            
            // Check for .dbproj
            if (!File.Exists(Path.Combine(_workspaceRoot, $"{_projectName}.dbproj")))
            {
                missing.Add($"{_projectName}.dbproj - Db project file");
            }
            
            return missing;
        }
        
        /// <summary>
        /// Cleans the build folder.
        /// </summary>
        public void Clean()
        {
            if (Directory.Exists(_buildFolder))
            {
                Directory.Delete(_buildFolder, recursive: true);
                OnBuildOutput("✓ Build folder cleaned");
            }
        }
        
        #region Private Methods
        
        private string FindWindowsSdkBin()
        {
            // rc.exe and mt.exe live in the Windows Kits SDK bin folder
            var kitsRoot = @"C:\Program Files (x86)\Windows Kits\10\bin";
            if (!Directory.Exists(kitsRoot)) return string.Empty;

            // Take the highest version number
            var latestVersion = Directory.GetDirectories(kitsRoot)
                .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+\.\d+"))
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (latestVersion == null) return string.Empty;

            var bin64 = Path.Combine(latestVersion, "x64");
            return File.Exists(Path.Combine(bin64, "rc.exe")) ? bin64 : string.Empty;
        }

        private string FindNinja()
        {
            // Check PATH first
            var fromPath = FindInPath("ninja");
            if (!string.IsNullOrEmpty(fromPath)) return fromPath;

            // Known vcpkg download locations
            var candidates = new[]
            {
                @"E:\Programming\vcpkg\downloads\tools\ninja-1.13.2-windows\ninja.exe",
                @"C:\vcpkg\downloads\tools\ninja-1.13.2-windows\ninja.exe",
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            // Scan vcpkg\downloads\tools for any ninja-* folder
            var vcpkg = FindVcpkg();
            if (!string.IsNullOrEmpty(vcpkg))
            {
                var toolsDir = Path.Combine(vcpkg, "downloads", "tools");
                if (Directory.Exists(toolsDir))
                {
                    foreach (var dir in Directory.GetDirectories(toolsDir, "ninja-*"))
                    {
                        var exe = Path.Combine(dir, "ninja.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
            }

            return string.Empty;
        }

        private string FindVcpkg()
        {
            // Check environment variable first
            var env = Environment.GetEnvironmentVariable("VCPKG_ROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;

            var candidates = new[]
            {
                @"E:\Programming\vcpkg",
                @"C:\vcpkg",
                @"C:\src\vcpkg",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "vcpkg"),
            };
            foreach (var c in candidates)
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "vcpkg.exe")))
                    return c;

            return string.Empty;
        }

        private string FindCMake()
        {
            // Try PATH first
            var cmakePath = FindInPath("cmake");
            if (!string.IsNullOrEmpty(cmakePath))
            {
                return cmakePath;
            }
            
            // Common installation locations
            var commonPaths = new[]
            {
                @"C:\Program Files\CMake\bin\cmake.exe",
                @"C:\Program Files (x86)\CMake\bin\cmake.exe"
            };
            
            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return "cmake"; // Hope it's in PATH
        }
        
        private string FindCompiler()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Look for MSVC
                var msvc = FindInPath("cl.exe");
                if (!string.IsNullOrEmpty(msvc))
                {
                    return msvc;
                }
                
                // Visual Studio installations
                var vsPaths = new[]
                {
                    @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\MSVC",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC"
                };
                
                foreach (var vsPath in vsPaths)
                {
                    if (Directory.Exists(vsPath))
                    {
                        var msvcDir = Directory.GetDirectories(vsPath).OrderByDescending(d => d).FirstOrDefault();
                        if (msvcDir != null)
                        {
                            var clPath = Path.Combine(msvcDir, "bin", "Hostx64", "x64", "cl.exe");
                            if (File.Exists(clPath))
                            {
                                return clPath;
                            }
                        }
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return FindInPath("g++") ?? "g++";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return FindInPath("clang++") ?? "clang++";
            }
            
            return string.Empty;
        }
        
        private string? FindInPath(string executableName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }
            
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var exePath = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(exePath))
                {
                    return exePath;
                }
                
                // Try with extension on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var exePathExe = exePath + ".exe";
                    if (File.Exists(exePathExe))
                    {
                        return exePathExe;
                    }
                }
            }
            
            return null;
        }
        
        private string FindBuiltExecutable()
        {
            var binFolder = Path.Combine(_buildFolder, "bin", Configuration);
            var exeBaseName = _projectName;
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{exeBaseName}.exe" : exeBaseName;
            
            var exePath = Path.Combine(binFolder, exeName);
            if (File.Exists(exePath))
            {
                return exePath;
            }
            
            // Try alternative locations
            var alternatives = new[]
            {
                Path.Combine(_buildFolder, Configuration, exeName),
                Path.Combine(_buildFolder, "bin", exeName),
                Path.Combine(_buildFolder, exeName)
            };
            
            foreach (var alt in alternatives)
            {
                if (File.Exists(alt))
                {
                    return alt;
                }
            }
            
            return string.Empty;
        }
        
        private BuildResult RunCommand(string command, IEnumerable<string> args, bool waitForExit = true, IReadOnlyDictionary<string, string>? extraEnv = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                WorkingDirectory = _workspaceRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            // Apply captured MSVC environment (from vcvarsall.bat) so LIB, INCLUDE, PATH are all correct
            if (extraEnv != null)
            {
                foreach (var kv in extraEnv)
                    startInfo.Environment[kv.Key] = kv.Value;
            }
            
            var output = new StringBuilder();
            var error = new StringBuilder();
            
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        OnBuildOutput(e.Data);
                    }
                };
                
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                        OnBuildOutput($"ERROR: {e.Data}");
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                if (waitForExit)
                {
                    process.WaitForExit();
                }
                
                return new BuildResult
                {
                    ExitCode = waitForExit ? process.ExitCode : 0,
                    Output = output.ToString() + error.ToString()
                };
            }
        }
        
        private void ParseBuildErrors(string output)
        {
            // Parse compiler errors and report them
            var errorPattern = new Regex(
                @"(?<file>.*?\.(?:cpp|h))\((?<line>\d+)\): error (?<code>\w+): (?<message>.*)",
                RegexOptions.Compiled
            );
            
            foreach (var line in output.Split('\n'))
            {
                var match = errorPattern.Match(line);
                if (match.Success)
                {
                    var file = match.Groups["file"].Value;
                    var lineNumber = int.Parse(match.Groups["line"].Value);
                    var code = match.Groups["code"].Value;
                    var message = match.Groups["message"].Value;
                    
                    OnBuildError($"Error {code}", message, lineNumber, file);
                }
            }
        }
        
        private void OnBuildOutput(string message)
        {
            BuildOutput?.Invoke(this, new BuildOutputEventArgs(message));
        }
        
        private void OnBuildError(string title, string message, int errorCode = -1, string? file = null, int lineNumber = 0)
        {
            BuildError?.Invoke(this, new BuildErrorEventArgs(title, message, errorCode, file, lineNumber));
        }
        
        #endregion
    }
    
    /// <summary>
    /// Result of a command execution.
    /// </summary>
    public class BuildResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Event args for build output.
    /// </summary>
    public class BuildOutputEventArgs : EventArgs
    {
        public string Message { get; }
        
        public BuildOutputEventArgs(string message)
        {
            Message = message;
        }
    }
    
    /// <summary>
    /// Event args for build errors.
    /// </summary>
    public class BuildErrorEventArgs : EventArgs
    {
        public string Title { get; }
        public string Message { get; }
        public int ErrorCode { get; }
        public string? File { get; }
        public int LineNumber { get; }
        
        public BuildErrorEventArgs(string title, string message, int errorCode = -1, string? file = null, int lineNumber = 0)
        {
            Title = title;
            Message = message;
            ErrorCode = errorCode;
            File = file;
            LineNumber = lineNumber;
        }
    }
}