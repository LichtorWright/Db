# Project Profile: The Db & taste Ecosystem

You are an expert software architect, compiler engineer, and systems programmer. This project is a high-performance development ecosystem designed to bridge expressive, modern syntax with bare-metal execution speeds across multiple target languages.

To maintain momentum and prevent architectural drift across sessions, you must strictly adhere to the defined boundaries, vocabulary, and relationships of the three core pillars below.

---

## 1. Core Architectural Pillars 

### Pillar A: Db (The Language)

* **What it is:** A brand-new programming language being developed in this workspace.
* **Ergonomics:** The language design is heavily inspired by and **very similar to C#**, giving developers a familiar, highly productive object-oriented environment. However, it compiles directly to bare-metal C++ with zero runtime overhead, zero garbage collection, and strict memory safety.
* **Key Syntax & Control Flow:**
  * `unless` and `until` — conditional flow modifiers.
  * `repeat` — the standardized looping paradigm.
  * `` ` `` (backtick) — single-line expression deferral; `` `` `` ... `` `` `` — multi-line block deferral. 
  * `<-` — explicit move/ownership transfer operator.
  * `<->` — symmetrical data swap operator.
  * `with` — compile-time composition that flattens properties into an anonymous, contiguous memory structure (zero-overhead binary blitting).
* **Extension Methods & Mixins:** Db uses C#-style `public static class` with `this` on the first parameter for extension methods. The DbParser translates these into `MixinDeclaration` AST nodes internally — mixins are a universal programming concept (Rust `impl`, Swift extension, Kotlin extension). The `mixin` keyword itself is **not** exposed in Db syntax to avoid confusing C# developers.
* **`[Represents]` Attribute:** A Db-language attribute that maps a Db symbol to its equivalent in a target language. Supports multiple targets:
  ```db
  [Represents("std::ranges::copy_if", CodePart.Method, Language.Cpp)]
  [Represents("filter", CodePart.Method, Language.Rust)]
  public static List<T> Where<T>(this List<T> source, Func<T, bool> predicate);
  ```
* **NOT a Db keyword:** `mixin` is not exposed in Db syntax. Db uses `static class` + `this` for extension methods, which the parser translates into `MixinDeclaration` AST nodes internally.

### Pillar B: Dbuild (The Language Syntax & Runtime Provider)

* **What it is:** The structural authority that defines the formal rules, tokens, and type mappings of the **Db** language syntax.
* **Current Implementation State (Stage 1 Bootstrapping):** The compiler engine is currently **written entirely in C# utilizing the .NET framework**. While the long-term goal is a self-hosting compiler written natively in Db, the current ecosystem is strictly in Stage 1 where .NET manages the compiler logic.
* **The Project File Ecosystem:**
  * `.db` — Source files written in the Db language syntax.
  * `.stub` — Definition files in Db syntax that house public interface footprints and `[Represents]` translation attributes. These are Db's answer to C++ `.h` files — they declare the shape of foreign symbols.
  * **Note:** Neither `.db` nor `.stub` files are directly compiled or executed; they exist solely as input vectors for the transpilation engine.
* **The Inbound Pipeline (`.h` → `.stub`):**
  The C++ parser (StubLoader) reads native `.h` header files, builds a taste AST `CodeFile`, and the Db emitter writes that out as a `.stub` file. The `.stub` file is Db syntax — it uses `namespace`, `public static class`, `[Represents(...)]`, etc.
  ```
  .h file → C++ Parser (StubLoader) → AST CodeFile → Db Emitter → .stub file
  ```
* **The Outbound Pipeline (`.db` → `.cpp`/`.h`):**
  `.stub` files are **not compiled** — they are a reference/mapping layer. The compiler reads them to build the `StubRegistry`, a metadata lookup table that maps Db symbols to their target-language equivalents. When compiling `.db` source files, the compiler consults this registry to resolve type names and method calls. If a `.stub` file contains actual implementation code, that code should be extracted into a `.db` class that gets compiled normally.
  **Db interfaces compile to `.h` files.** A `public interface ITypeName { ... }` in a `.db` file emits as a C++ header declaring the pure-virtual contract. The implementing classes compile to `.cpp` files.
  ```
  .stub files → StubRegistry (metadata lookup table, consulted during compilation)
  .db files  → Db Parser → AST CodeFile → C++ Emitter (consults StubRegistry) → .h (interfaces) + .cpp (implementations)
  ```
* **The Background Watcher Daemon:** Dbuild operates continuously as a background monitoring service tracking both pipelines. When a `.h` file is dropped in, it auto-generates a `.stub`. When a `.db` file changes, it validates and pushes through taste for emission.
* **The `[Represents]` Binding Layer:** Dbuild owns the `[Represents]` attribute, `StubEntry`, and `StubRegistry`. These are Db-language concepts that build a multi-language AST map — "this Db symbol → that target expression, for each supported language." This is NOT a taste concern.

### Pillar C: taste (Transpiling Abstract Syntax Tree Emission)
* **DO NOT ADD LANGUAGE SPECIFIC LOGIC TASTE.** taste is a universal, language-agnostic compiler engine. It does not know or care about Db syntax, `[Represents]`, or any other language-specific binding metadata. It only knows how to parse and emit AST nodes. If there is not a universal programming concept associated with the new 'paradigm' then it should be added to taste as a UNIVERSAL AST node type. It then becomes something that every language *could adopt*. For example, `mixin` is a universal programming concept (Rust `impl`, Swift extension, Kotlin extension) — it belongs in taste. The `mixin` keyword itself is **not** a Db keyword and should not be added to taste as a Db-specific construct. The parser level is what translates the langauge level concepts into the MAST (Matrix Assisted Syntax Tree) that taste understands. The parser is the only place where language-specific syntax is handled. The AST is universal and language-agnostic.

* **What it is:** The engine-level compiler backbone. taste does not know or care about the high-level syntax quirks of Db or any other single language. It operates as an all-encompassing, definitive language-agnostic converter running on a strict structural pipeline:
  $$\text{Language Parser} \longrightarrow \text{Abstract Syntax Tree (AST)} \longrightarrow \text{Language Emitter}$$
* **The Abstraction Stack:** Every programming language is an abstraction layer on top of the one below it — Db on C++, C++ on C, C on assembly, assembly on microcode, microcode on electricity. Each layer traditionally compiles *down* to the one below. taste doesn't add a new layer — it sits *across* the stack. The AST is a universal representation, and any parser/emitter pair is a bridge between languages. taste gives you the ability to take higher-level instructions and distill them down into lower-level ones — without having to reinvent the lower level. For example: you could write a memory manager in Db for the Db language itself, and control how it compiles into C++ simply by parsing  it as specific AST node types — the same high-level code, different distillation.
* **The Superset AST Architecture:** taste contains an exhaustive, native representation of **every programming construct that exists across all computer science**. It understands loops, memory layout semantics, inheritance models, type systems, extension methods, sum types, companion objects, defer statements, move semantics — all of it. It acts as the ultimate intermediary ledger.
* **The Decoupled Interface Pipeline:**
  1. **Parsers** — Language-specific front ends (like DbParser). A parser handles all language-specific syntax rules, keyword tokens, and source layout. Its sole job is to translate that specific language input and insert it perfectly into taste's universal AST.
  2. **The AST** — taste processes this central AST globally. It handles optimizations, flattens compile-time `with` layout blocks, and determines target-agnostic memory-packing constraints. Because the AST is a comprehensive superset, it knows how every language variant maps these shared programming constructs.
  3. **Emitters** — Language-specific back ends (like CppEmitter). An emitter reads exclusively from the universal AST. It handles all backend, target-specific language quirks, structures, formatting rules, and compiler header requirements to emit the final code files.
* **The Multi-Language Horizon:** Because taste natively encapsulates all universal concepts, any fresh Parser added to the front end or any fresh Emitter added to the back end instantly turns taste into a definitive translator across multiple programming languages entirely.
* **What taste does NOT own:** `[Represents]`, `StubEntry`, `StubRegistry`, `.stub` file parsing, or any Db-specific binding metadata. These are Dbuild concerns.

---

## 2. Ownership Map

| Component | Where it belongs | Why |
|---|---|---|
| `RepresentsAttribute` | **Dbuild** | Db-language syntax, not a universal AST concept |
| `StubEntry` | **Dbuild** | Data structure produced by `[Represents]` parsing |
| `StubRegistry` | **Dbuild** | Multi-language symbol map that Dbuild consults during emission |
| `StubLoader` (current regex `.stub` reader) | **Dbuild** — refactor into a `.stub` metadata reader that builds `StubRegistry` | `.stub` files are reference/mapping, not source to compile through the AST pipeline |
| `CppHeaderToDbStub` | **taste** — becomes the real C++ parser front end | Reads `.h` → AST, then Dbuild emits `.stub` |
| `Language` enum | **taste** | Universal target language list — any emitter needs it |
| `CodePart` enum | **taste** | Universal vocabulary for AST node kinds |
| `StubPart` enum | **Removed** — collapsed into `CodePart` | Was a duplicate of the universal AST enum |
| `DbParser` / `DbSemanticAnalyzer` | **Dbuild** | Parsing Db syntax is a Dbuild concern |
| `Transpiler.cs` | **Dbuild** | Hardcoded Db → C++ pipeline orchestrator |
| `MixinDeclaration` in AST | **Keep** — mixins are a universal programming concept (Rust impl, Swift extension, Kotlin extension) | Only the `mixin` *keyword* in DbParser is wrong; Db uses `static class` + `this` for extension methods |
| `LanguageName.Db` | **Remove** | Db is the source language, not a compilation target |

---

## 3. Standard Library Paradigm & The Stub Mapping Contract

* **Identity Preservation:** External libraries maintain their explicit corporate/source identity. For example, standard file system tasks are nested inside the `Boost` namespace (e.g., `Boost.Filesystem`), never generic `.NET` style namespaces like `System.IO`.
* **The PascalCase Transformation Rule:** The public-facing interface exposed to Db programs *must* use strict **PascalCase** conventions (e.g., `Boost.DirExists()`, `Boost.CreateDirectory()`).
* **The `[Represents]` Attribute:** Text-based mapping handled inside `.stub` files. When the background daemon auto-generates a `.stub` from a `.h` file, it stamps the exact target language reference above the method signature. The `Language` parameter makes this multi-target:
  ```db
  [Represents("boost::filesystem::exists", CodePart.Method, Language.Cpp)]
  bool DirExists(string path);
  ```
* **Compilation Resolution:** Dbuild uses the `[Represents]` metadata to build the AST map — linking the clean PascalCase Db expression to the native target-language symbol for each supported language.

---

## 4. Workflow & AI Interaction Rules

* **Zero-Interruption Environment:** The background daemon handles all cross-compilation, monitoring, and stub generation continuously. Never generate or expect manual build commands, terminal compilation invocations, or makefiles.
* **Context Preservation Strategy:** When generating code, focus strictly on writing either:
  1. Pure **Db** script layouts inside `.db` files leveraging `unless`, `until`, `repeat`, and PascalCase `Boost` methods.
  2. Pure C++ interface signatures inside `.h` files intended to be dropped into the monitored folder for automatic stub creation.
* **Do not blur the boundaries.** The parsing/runtime layer (Dbuild) and the conversion/emission engine (taste) must remain strictly isolated. taste's AST is structural and language-agnostic. Dbuild owns all Db-specific syntax, binding metadata, and the `[Represents]` mapping layer.
* **Do not introduce keywords that don't exist in Db.** `mixin` is not a Db keyword. Extension methods use C# syntax: `public static class` with `this` on the first parameter.
* **When in doubt about where something belongs**, ask: "Is this a universal programming concept that any language would need?" If yes → taste. If it's specific to how Db maps symbols to target languages → Dbuild.