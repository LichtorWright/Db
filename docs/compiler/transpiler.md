# Transpiler

`taste.Transpiler` is the single entry point for a complete Db → C++ compilation.  
Source: `taste/Transpiler.cs`

---

## Usage

```csharp
string cpp = Transpiler.Transpile(dbSourceText, filePath);
```

Or, from a file:

```csharp
string cpp = Transpiler.TranspileFile(path);
```

---

## Pipeline

```
string source
      │
      ▼
DbParser.Parse()            → CodeFile (AST)
      │
      ▼
DbSemanticAnalyzer.Analyze() → (mutates CodeFile — stub resolution, type checks)
      │
      ▼
CppEmitter.Emit(CodeFile)   → string (C++ source)
```

---

## Error Handling

Exceptions thrown by the parser or semantic analyser bubble up from `Transpile`.  
The calling tool (`dbuild`) catches them and reports them to the user with source location context.

---

## Dbuild Integration

`dbuild` wraps `Transpiler` in a file watcher (`DbFileWatcher`).
On every save:

1. Re-read the `.db` file.
2. Call `Transpiler.TranspileFile`.
3. Write the result to the corresponding `.cpp` / `.h` output path.
4. Report any errors to the console / extension.
