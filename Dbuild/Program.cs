using System;
using System.IO;
using taste;
using taste.Emit;

namespace Dbuild
{
    class Program
    {
        /// <summary>
        /// Discovers the project name by finding the .dbproj file in the workspace root.
        /// The project folder name matches the .dbproj filename (e.g. MyGame.dbproj → MyGame/).
        /// </summary>
        static string? DiscoverProjectName(string workspaceRoot)
        {
            var dbprojFiles = Directory.GetFiles(workspaceRoot, "*.dbproj");
            if (dbprojFiles.Length == 0)
            {
                Console.WriteLine("Error: No .dbproj file found in the current directory.");
                Console.WriteLine("Are you in a Db project root?");
                return null;
            }
            if (dbprojFiles.Length > 1)
            {
                Console.WriteLine("Error: Multiple .dbproj files found. Only one project per directory is supported.");
                return null;
            }
            return Path.GetFileNameWithoutExtension(dbprojFiles[0]);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string command = args[0];

            // CLI flags
            switch (command)
            {
                case "-monitor":
                    StartMonitor(args);
                    break;

                case "-check":
                    Console.WriteLine("[-check] Diagnostics not yet implemented.");
                    break;

                case "-stub":
                    HandleStubCommand(args);
                    break;

                case "-new":
                    HandleNewCommand(args);
                    break;

                case "-add":
                    Console.WriteLine("[-add] Not yet implemented.");
                    break;

                case "--configure":
                case "--build":
                case "--configure-and-build":
                case "--clean":
                case "--run-debug":
                case "--check-prereqs":
                    HandleBuildCommand(command);
                    break;

                default:
                    // Legacy: transpile a single file
                    if (args.Length >= 2 && !command.StartsWith("-"))
                    {
                        TranspileFile(args[0], args[1]);
                    }
                    else
                    {
                        Console.WriteLine($"Unknown command: {command}");
                        PrintUsage();
                        Environment.Exit(1);
                    }
                    break;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("dbuild — the Db build orchestration tool");
            Console.WriteLine();
            Console.WriteLine("Usage: dbuild <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  -new project <name>   Scaffold a new Db project");
            Console.WriteLine("  -add file <name> <t>  Add a file from a template (not yet implemented)");
            Console.WriteLine("  -stub <file.db>       Generate a .stub file from a .db source file");
            Console.WriteLine("  -monitor              Start file watchers for live transpilation");
            Console.WriteLine("  -check                Run diagnostics on the project (not yet implemented)");
            Console.WriteLine();
            Console.WriteLine("Build commands:");
            Console.WriteLine("  --configure           Configure CMake build system");
            Console.WriteLine("  --build               Build C++ code");
            Console.WriteLine("  --configure-and-build Configure and build");
            Console.WriteLine("  --clean               Clean build folder");
            Console.WriteLine("  --run-debug           Run with debugging");
            Console.WriteLine("  --check-prereqs       Check prerequisites");
            Console.WriteLine();
            Console.WriteLine("Legacy:");
            Console.WriteLine("  dbuild <input.db> <output.cpp>   Transpile a single file");
        }

        static void StartMonitor(string[] args)
        {
            string workspaceRoot = Directory.GetCurrentDirectory();
            string? projectName = DiscoverProjectName(workspaceRoot);
            if (projectName == null)
            {
                Environment.Exit(1);
                return;
            }

            string projectFolder = Path.Combine(workspaceRoot, projectName);
            string headerFolder = Path.Combine(projectFolder, "Included Libraries", "Headers");
            string stubsFolder = Path.Combine(projectFolder, "Stubs");
            string dbFolder = projectFolder;
            string generatedFolder = Path.Combine(projectFolder, "Pregen");

            // Load stub registry once; shared across all transpilation calls
            StubRegistry registry = Directory.Exists(stubsFolder)
                ? new StubLoader().LoadDirectory(stubsFolder)
                : new StubRegistry();
            Console.WriteLine($"[-monitor] Loaded stubs from: {stubsFolder}");

            using (var headerWatcher = new ForeignFolderWatcher(new CppHeaderToDbStub(), headerFolder, stubsFolder))
            using (var dbWatcher = new DbFileWatcher(dbFolder, generatedFolder, registry))
            {
                headerWatcher.Start();
                dbWatcher.Start();

                Console.WriteLine($"[-monitor] Header watcher: {headerFolder} → {stubsFolder}");
                Console.WriteLine($"[-monitor] Db auto-gen: {dbFolder} → {generatedFolder}/*.g.cpp");
                Console.WriteLine();
                Console.WriteLine("Monitoring active. Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void HandleNewCommand(string[] args)
        {
            // -new project <name> [options]
            if (args.Length < 3 || args[1].ToLower() != "project")
            {
                Console.WriteLine("Usage: dbuild -new project <ProjectName>");
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  dbuild -new project MyGame");
                Environment.Exit(1);
                return;
            }

            string projectName = args[2];

            // Validate project name
            if (string.IsNullOrWhiteSpace(projectName) || projectName.Contains(' ') || projectName.Contains('.'))
            {
                Console.WriteLine($"Error: Invalid project name '{projectName}'.");
                Console.WriteLine("Project names must not contain spaces or dots.");
                Environment.Exit(1);
                return;
            }

            string workspaceRoot = Directory.GetCurrentDirectory();
            string projectFolder = Path.Combine(workspaceRoot, projectName);
            string dbprojPath = Path.Combine(workspaceRoot, $"{projectName}.dbproj");

            // Check if project already exists
            if (Directory.Exists(projectFolder) || File.Exists(dbprojPath))
            {
                Console.WriteLine($"Error: A project named '{projectName}' already exists in this directory.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"[-new project] Creating project '{projectName}'...");

            // Create project folder structure
            Directory.CreateDirectory(projectFolder);
            Directory.CreateDirectory(Path.Combine(projectFolder, "Source", "Db"));
            Directory.CreateDirectory(Path.Combine(projectFolder, "Stubs"));
            Directory.CreateDirectory(Path.Combine(projectFolder, "Included Libraries", "Headers"));
            Directory.CreateDirectory(Path.Combine(projectFolder, "Pregen"));
            Directory.CreateDirectory(Path.Combine(projectFolder, "Build"));

            // Copy Db runtime library from template
            string templateDir = Path.Combine(AppContext.BaseDirectory, "NewProjectDefaultDirectory", "Project");
            CopyTemplateFiles(templateDir, projectFolder);

            // Write .dbproj manifest
            File.WriteAllText(dbprojPath,
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project name=""{projectName}"" targetLanguage=""Cpp"">
  <!-- Source paths (relative to this file) -->
  <SourcePath>{projectName}/Source</SourcePath>
  <StubPath>{projectName}/Stubs</StubPath>
  <HeaderPath>{projectName}/Included Libraries/Headers</HeaderPath>
  <GeneratedPath>{projectName}/Pregen</GeneratedPath>
  <BuildPath>{projectName}/Build</BuildPath>

  <!-- Memory profile: RAII (default), Safe, or Manual -->
  <MemoryProfile>RAII</MemoryProfile>

  <!-- C++ standard for emission -->
  <CppStandard>C++17</CppStandard>

  <!-- Output type: Exe or Lib -->
  <OutputType>Exe</OutputType>

  <!-- Resource system (future: embedded, filesystem, pak) -->
  <!-- <Resources type=""filesystem"" root=""{projectName}/Assets"" /> -->
</Project>
");

            // Write a starter Main.db
            string mainDb = Path.Combine(projectFolder, "Source", "Main.db");
            File.WriteAllText(mainDb,
$@"// {projectName} — entry point

public class Program
{{
    public static void Main()
    {{
        // Hello from Db!
    }}
}}
");

            // Write a starter CMakeLists.txt
            string cmakePath = Path.Combine(projectFolder, "Build", "CMakeLists.txt");
            File.WriteAllText(cmakePath,
$@"cmake_minimum_required(VERSION 3.20)
project({projectName} LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Collect generated .cpp files from Pregen
file(GLOB DB_GENERATED ""${{CMAKE_CURRENT_SOURCE_DIR}}/../Pregen/*.g.cpp"")

add_executable({projectName} ${{DB_GENERATED}})
");

            Console.WriteLine($"  Created {dbprojPath}");
            Console.WriteLine($"  Created {projectFolder}/");
            Console.WriteLine($"    Source/Main.db");
            Console.WriteLine($"    Source/Db/  (Db runtime library)");
            Console.WriteLine($"    Stubs/");
            Console.WriteLine($"    Included Libraries/Headers/");
            Console.WriteLine($"    Pregen/");
            Console.WriteLine($"    Build/CMakeLists.txt");
            Console.WriteLine();
            Console.WriteLine($"Project '{projectName}' created. To start coding:");
            Console.WriteLine($"  cd {projectFolder}");
            Console.WriteLine($"  dbuild -monitor");
            Environment.Exit(0);
        }

        /// <summary>
        /// Copies template files (Stubs, Source/Db, Included Libraries) from the
        /// embedded NewProjectDefaultDirectory into the newly created project.
        /// </summary>
        static void CopyTemplateFiles(string templateDir, string projectFolder)
        {
            if (!Directory.Exists(templateDir))
                return;  // template not available — create empty structure only

            // Copy Stubs/
            string srcStubs = Path.Combine(templateDir, "Stubs");
            if (Directory.Exists(srcStubs))
                CopyDirectory(srcStubs, Path.Combine(projectFolder, "Stubs"));

            // Copy Source/Db/
            string srcDb = Path.Combine(templateDir, "Source", "Db");
            if (Directory.Exists(srcDb))
                CopyDirectory(srcDb, Path.Combine(projectFolder, "Source", "Db"));

            // Copy Included Libraries/
            string srcHeaders = Path.Combine(templateDir, "Included Libraries");
            if (Directory.Exists(srcHeaders))
                CopyDirectory(srcHeaders, Path.Combine(projectFolder, "Included Libraries"));
        }

        /// <summary>
        /// Recursively copies a directory and all its contents.
        /// </summary>
        static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        /// <summary>
        /// Handles the -stub command: generates a .stub file from a .db source file.
        /// Usage: dbuild -stub <file.db> [output.stub]
        /// If no output path is specified, writes next to the input file.
        /// </summary>
        static void HandleStubCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dbuild -stub <file.db> [output.stub]");
                Console.WriteLine();
                Console.WriteLine("Generates a .stub (interface-only) file from a .db source file.");
                Console.WriteLine("Strips method bodies, private members, and local variables.");
                Console.WriteLine("If no output path is specified, writes <file>.stub next to the input.");
                Environment.Exit(1);
                return;
            }

            string inputPath = args[1];
            string? outputPath = args.Length >= 3 ? args[2] : null;

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found: {inputPath}");
                Environment.Exit(1);
                return;
            }

            try
            {
                string result = StubEmitter.EmitStubToFile(inputPath, outputPath);
                Console.WriteLine($"Stub generated: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating stub: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        static void HandleBuildCommand(string command)
        {
            var workspaceRoot = Directory.GetCurrentDirectory();
            string? projectName = DiscoverProjectName(workspaceRoot);
            if (projectName == null)
            {
                Environment.Exit(1);
                return;
            }

            var buildSystem = new CppBuildSystem(workspaceRoot, projectName);

            // Wire up event handlers
            buildSystem.BuildOutput += (s, e) => Console.WriteLine(e.Message);
            buildSystem.BuildError += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{e.Title}: {e.Message}");
                if (!string.IsNullOrEmpty(e.File))
                {
                    Console.WriteLine($"  at {e.File}:{e.LineNumber}");
                }
                Console.ResetColor();
            };

            switch (command.ToLower())
            {
                case "--configure":
                    var configResult = buildSystem.Configure();
                    Environment.Exit(configResult ? 0 : 1);
                    break;

                case "--build":
                    var buildResult = buildSystem.Build();
                    Environment.Exit(buildResult ? 0 : 1);
                    break;

                case "--configure-and-build":
                    var fullResult = buildSystem.ConfigureAndBuild();
                    Environment.Exit(fullResult ? 0 : 1);
                    break;

                case "--clean":
                    buildSystem.Clean();
                    Environment.Exit(0);
                    break;

                case "--run-debug":
                    var runResult = buildSystem.RunWithDebug();
                    Environment.Exit(runResult);
                    break;

                case "--check-prereqs":
                    var missing = buildSystem.CheckPrerequisites();
                    if (missing.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Missing prerequisites:");
                        Console.ResetColor();
                        foreach (var item in missing)
                        {
                            Console.WriteLine($"  • {item}");
                        }
                        Environment.Exit(1);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ All prerequisites are installed");
                        Console.ResetColor();
                        Environment.Exit(0);
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown build command: {command}");
                    Environment.Exit(1);
                    break;
            }
        }

        /// <summary>
        /// Transpiles a Db (.db) file to C++ (.cpp) format using taste.
        /// Attempts to discover and load stubs from the project stubs folder.
        /// </summary>
        static void TranspileFile(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found: {inputPath}");
                Environment.Exit(1);
            }

            // Try to load stubs from the project root (walk up from input file)
            StubRegistry? registry = null;
            string? dir = Path.GetDirectoryName(Path.GetFullPath(inputPath));
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "Stubs");
                if (Directory.Exists(candidate))
                {
                    registry = new StubLoader().LoadDirectory(candidate);
                    break;
                }
                dir = Path.GetDirectoryName(dir);
            }

            try
            {
                Transpiler.TranspileFile(inputPath, outputPath, registry);
                Console.WriteLine($"Transpilation successful. Output written to {outputPath}");
            }
            catch (TranspilerException ex)
            {
                Console.WriteLine($"Error [E{ex.ErrorCode}]: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.Suggestion))
                {
                    Console.WriteLine($"Suggestion: {ex.Suggestion}");
                }
                Console.WriteLine($"Location: {inputPath}:{ex.LineNumber}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}