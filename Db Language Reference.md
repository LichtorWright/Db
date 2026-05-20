# Db Language Reference

## What Db Is

Db is a systems programming language with **C# syntax** that compiles to **bare-metal C++**. It is not C#. It is not Python. It looks like C# but the runtime is C++.

**The golden rule:** Every line of Db code must be valid in a world where the only libraries available are C++ standard library, Boost, and Db's own runtime.

---

## Syntax Rules

- **Semicolons are mandatory.** Every statement ends with `;`. This is not Python.
- **Parentheses around conditions.** `if (x)`, `unless (y)`, `while (z)`, `foreach (var a in b)`. This is C# syntax.
- **Braces for multi-statement bodies.** `if (x) { a; b; }` — not indentation-based.

## Db-Specific Syntax

| Syntax | Meaning | Compiles To |
|---|---|---|
| `unless (cond)` | `if (!(cond))` | `if (!(cond))` |
| `until (cond)` | `while (!(cond))` | `while (!(cond))` |
| `repeat N` | counted loop | `for (auto _i = 0; _i < N; ++_i)` |
| `` `expr; `` | defer to scope exit | RAII scope guard |
| `` ``block;`` `` | defer block to scope exit | RAII scope guard |
| `<-` | move semantics | `std::move()` |
| `<->` | swap | `std::swap()` |
| `with` | composition / mixin | CRTP or inline expansion |
| `log stream for Type.Level payload;` | structured logging | `stream.Write(Type::Level, ...)` |

---

## What Db Is NOT

| ❌ Don't do this | ✅ Do this instead |
|---|---|
| `typeof(T)` | Use explicit typed overloads (`GetInt()`, `GetFloat()`) |
| `object` / boxing | Use concrete types or templates |
| `default` as generic fallback | Use explicit default values |
| C# BCL types (`List<T>` from System.Collections) | `List<T>` maps to `std::vector` via stubs |
| `out` parameters (C# style) | `out` compiles to pointer/reference params |
| `string.Format()` / `$""` interpolation | String concatenation with `+` operator |
| `System.Console.WriteLine()` | `Console.Write()` via Standard stubs |
| `System.IO.File` | `File.Exists()` via Boost.IO stubs |
| Python-style no semicolons | Every statement ends with `;` |
| Python-style no parens on conditions | `if (x)`, `while (x)`, `unless (x)` |

---

## Three-Pillar Architecture

### Pillar A — Db (the language)
Parser-level concerns only. Keywords, tokens, syntax recognition.
- **Namespace:** `taste.Parse.Db`
- **Files:** `DbParser.cs`, `DbSemanticAnalyzer.cs`, `DbKeywords.cs`

### Pillar B — Dbuild (the toolchain)
Language-specific runtime and tooling. Owns stubs, transpilation, build system, project scaffolding.
- **Namespace:** `Dbuild`
- **Files:** `StubEntry.cs`, `StubLoader.cs`, `StubRegistry.cs`, `Transpiler.cs` (DbTranspiler), `Program.cs`, `CppBuildSystem.cs`

### Pillar C — taste (the compiler engine)
Universal, language-agnostic AST and emission. **NEVER add language-specific logic here.**
- **Namespace:** `taste` (root), `taste.Emit`, `taste.Emit.Cpp`, `taste.Parse`
- **Files:** `Model.cs`, `LanguageMatrix.cs`, `Emitter.cs`, `CppEmitter.cs`, `Parser.cs`, `KnownTypes.cs`, `Transpiler.cs`

**The critical test:** "Is this a universal programming concept any language would need?" → taste. "Is this specific to how Db maps symbols to C++?" → Dbuild. "Is this a language-specific keyword/token?" → Db (Pillar A).

---

## Library Structure

### Standard (C++ std lib bindings)
Stubs that declare what C++ already has. These are `.stub` files — no implementation, just surface area.
- **Namespace:** `Standard`
- **Location:** `Stubs/Standard/`
- **Examples:** `String.stub`, `Vector.stub`, `Dictionary.stub`, `FileStream.stub`, `SharedPtr.stub`

### Boost (C++ Boost bindings)
Stubs for Boost library types.
- **Namespace:** `Boost.IO`, `Boost.Net`, etc.
- **Location:** `Stubs/BOOST/`
- **Examples:** `File.stub` (boost::filesystem), `IoContext.stub` (boost::asio)

### Db (Db's own runtime library)
Real Db code, written *in* Db, transpiled alongside your project. High-level abstractions built on Standard stubs.
- **Namespace:** `Db`
- **Location:** `Source/Db/`
- **Examples:** `Settings.db`, `Linq.db`, `Log.db`, `LogStream.db`, `MessageType.db`

---

## The `log` Statement

`log` is a first-class statement in Db (like `return` or `throw`), not a library call.

**Syntax:** `log <stream> for <MessageType.Level> <payload>;`

```db
var console = LogStream.Console();
log console for MessageType.Info "Server started";
log console for MessageType.Error new Exception();
log console for MessageType.Debug diagnosticData;
```

**What the compiler does:**
1. Parses into `LogStatement` AST node (stream, messageType, payload)
2. Injects source file and line number automatically
3. Emits C++: `console.Write(MessageType::Info, "[main.db:42] Server started");`
4. In release builds, `MessageType.Debug` logs are compiled out entirely (zero cost)

**Format tokens** (resolved by the logger, not the caller):
- `[%time]` — timestamp
- `[%date]` — date only
- `[%file]` — source file
- `[%line]` — line number
- `[%func]` — function name
- `[%thread]` — thread ID

---

## The `.dbproj` Manifest

Every Db project has a `.dbproj` XML file that declares project configuration:

```xml
<Project name="MyApp" targetLanguage="Cpp">
  <SourcePath>MyApp/Source</SourcePath>
  <StubPath>MyApp/Stubs</StubPath>
  <HeaderPath>MyApp/Included Libraries/Headers</HeaderPath>
  <GeneratedPath>MyApp/Pregen</GeneratedPath>
  <BuildPath>MyApp/Build</BuildPath>
  <MemoryProfile>RAII</MemoryProfile>
  <CppStandard>C++17</CppStandard>
  <OutputType>Exe</OutputType>
  <!-- <Resources type="filesystem" root="MyApp/Assets" /> -->
  <!-- <Logging level="info" output="console" /> -->
</Project>
```

---

## Memory Profiles

| Profile | Public | Private | Local | Use Case |
|---|---|---|---|---|
| RAII (default) | Shared | Unique | Stack | General purpose |
| Safe | Shared | Shared | Shared | Legacy compatibility |
| Manual | Raw | Raw | Stack | Embedded / kernel / hot paths |

Override per-field with `[MemoryManagement]` attribute.

---

## Project Structure (created by `-new project`)

```
MyApp/
  Source/
    Main.db          — entry point
    Db/              — Db runtime library (Settings, Linq, Log, etc.)
  Stubs/
    Standard/        — C++ std lib stubs
    BOOST/           — Boost library stubs
  Included Libraries/
    Headers/         — Third-party C++ headers
  Pregen/           — Generated .g.cpp output
  Build/
    CMakeLists.txt   — CMake build configuration
MyApp.dbproj         — Project manifest
```