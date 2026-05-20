# Architecture Update - Multi-Language Support

## Overview

The transpiler has been refactored to support multiple target languages (C++, D, etc.) with a modular architecture.

## New Components

### 1. C Namespace (`DbTranspiler.C`)

**Purpose**: Encapsulate C++-specific types and configurations for easy swapping with other language backends.

**Key Classes**:
- `CppTypes` - Contains all C++ type mappings, collection types, and language-specific constants

**Benefits**:
- Easy to add new language backends (e.g., `DTypes` for Digital Mars D)
- Centralized configuration for target language specifics
- Clean separation of concerns

### 2. Reverse Transpiler (`DbTranspiler.Reverse`)

**Purpose**: Convert C++ header files (.h) to C# Db stub files (.db) for IntelliSense support.

**Key Classes**:
- `CppHeaderToDbStub` - Converts C++ class/struct declarations to C# stubs
- `HeaderFolderWatcher` - Monitors Headers folder for .h files and auto-generates stubs

**Features**:
- Automatic detection of new/changed/deleted header files
- Generates C# properties from C++ member variables
- Generates C# method signatures from C++ declarations
- Strips implementation details (stub-only output)
- Preserves access modifiers and inheritance

### 3. File System Watcher

**Purpose**: Automatically generate C# stubs when C++ headers are added/modified.

**Folders**:
- `Headers/` - Place C++ .h files here
- `Stubs/` - Generated C# .db stub files appear here

**Workflow**:
1. Add C++ header to `Headers/` folder
2. FileSystemWatcher detects the change
3. Reverse transpiler generates C# stub
4. Stub appears in `Stubs/` folder
5. IntelliSense now works in C# editor!

## Usage

### Forward Transpilation (C# → C++)

```bash
dotnet run --project DbTranspiler -- input.db output.cpp
```

### Reverse Transpilation (C++ → C#)

Automatic via FileSystemWatcher, or manually:

```csharp
CppHeaderToDbStub.Convert("path/to/file.h", "path/to/output.db");
```

### File Watcher

Starts automatically when running without arguments:

```bash
dotnet run --project DbTranspiler
```

## Type Mappings

### C++ → C# (Reverse)

| C++ Type | C# Type |
|----------|---------|
| `int` | `int` |
| `float` | `float` |
| `double` | `double` |
| `bool` | `bool` |
| `std::string` | `string` |
| `void` | `void` |
| Custom classes | Preserved as-is |

### C# → C++ (Forward)

See existing documentation for complete mappings.

## Future Enhancements

### D Language Support

To add Digital Mars D support:

1. Create `DbTranspiler.D` namespace
2. Add `DTypes.cs` with D type mappings
3. Create `DTranspiler.cs` with D-specific conversion logic
4. Update Program.cs to accept `--target=d` flag

Example D type mappings:
```csharp
{"List", "std.array"},
{"string", "string"},
{"Console.WriteLine", "writeln"}
```

### Other Languages

The architecture supports adding:
- Java backend
- Rust backend  
- Go backend
- TypeScript backend

Each would have its own namespace and type mappings.

## Code Organization

```
DbTranspiler/
├── Program.cs              # Main orchestrator (updated to use C namespace)
├── C/                      # C++ backend
│   └── CppTypes.cs        # C++ type mappings
├── Reverse/                # Reverse transpilation
│   ├── CppHeaderToDbStub.cs  # C++ → C# converter
│   └── HeaderFolderWatcher.cs # File system monitoring
└── (future) D/            # D language backend
    └── DTypes.cs          # D type mappings
```

## XML Documentation

All public methods now include XML documentation comments (`/// <summary>`) for IntelliSense support throughout the codebase.

## Benefits

1. **Modularity**: Easy to swap/add language backends
2. **Bidirectional**: Can transpile both C#→C++ and C++→C#
3. **Developer Experience**: Automatic stub generation for IntelliSense
4. **Maintainability**: Clean separation of concerns
5. **Extensibility**: Ready for future language support

## Next Steps

1. ✅ Convert MD docs to XML comments
2. ✅ Implement reverse transpiler
3. ✅ Add FileSystemWatcher
4. ✅ Move C++ types to C namespace
5. ⏳ Implement D language support (future)
6. ⏳ Add more reverse transpilation features (templates, namespaces)
7. ⏳ Improve stub generation (XML docs from comments)
