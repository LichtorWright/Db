# Overview

Db (pronounced *D-flat*) is a statically typed, C#-syntax source language that transpiles to C++17/20.  
It lets you write expressive, readable code while targeting the full C++ ecosystem — zero-overhead abstractions, RAII, templates, and all.

---

## Design Goals

| Goal | Detail |
|---|---|
| **Familiar** | C# syntax — existing knowledge transfers immediately |
| **Zero-cost** | Every Db construct maps 1-to-1 to idiomatic C++ with no runtime overhead |
| **Expressive** | Novel syntax (defer, lazy, pipeline, repeat ranges) that C++ lacks natively |
| **Interoperable** | Stub files describe existing C++ headers; no wrapper layer needed |

---

## Build Pipeline

```
.db source files
      │
      ▼
 DbParser           — tokenises & parses into an AST (CodeFile)
      │
      ▼
 DbSemanticAnalyzer — type checking, stub resolution
      │
      ▼
 CppEmitter         — walks the AST, writes .cpp / .h output
      │
      ▼
 C++ compiler (clang / MSVC / GCC)
```

The host tool is **dbuild**, a file-watching CLI that recompiles on save.

---

## File Layout

```
Project/
  *.db              — Db source files
  Stubs/
    Standard/       — standard library stubs (string_view, vector, …)
    *.db            — stubs for C++ headers you depend on
```

Stubs use the `stub` keyword instead of `class` — they are IntelliSense-only declarations that are never emitted.

---

## Hello World

```db
namespace MyApp
{
    class Program
    {
        public static void Main()
        {
            Console.WriteLine("Hello, World!");
        }
    }
}
```

Emitted C++:

```cpp
namespace MyApp {

class Program {
public:
    static void Main() {
        std::cout << "Hello, World!" << std::endl;
    }
};

} // namespace MyApp
```
