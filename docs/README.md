# Db (D♭) Documentation

Db is a C#-syntax programming language that compiles to C++17/20.  
The compiler is called **taste** (Transpiler And Syntax Transformation Engine).

---

## Contents

### Language Reference
| Page | Topic |
|---|---|
| [01 — Overview](language/01-overview.md) | Philosophy, build pipeline, file layout |
| [02 — Types & Variables](language/02-types.md) | Primitives, inference, mutability |
| [03 — Control Flow](language/03-control-flow.md) | `if`, `unless`, `match`, `while`, `for`, `foreach` |
| [04 — Repeat Loops](language/04-repeat.md) | `repeat`, directional ranges, `repeat until` |
| [05 — Postfix Conditionals](language/05-postfix.md) | Trailing `if` / `unless` |
| [06 — Defer](language/06-defer.md) | Single defer, block defer |
| [07 — Lazy Fields](language/07-lazy.md) | Backtick lazy initialisation |
| [08 — Move & Swap](language/08-move-swap.md) | `<-` move, `<->` swap |
| [09 — Pipeline Operator](language/09-pipeline.md) | Comma-chained function composition |
| [10 — Classes & Members](language/10-classes.md) | Fields, properties, methods, events |
| [11 — Attributes](language/11-attributes.md) | `[Stack]`, `[Heap]`, `[Represents]`, etc. |
| [12 — Inline C++](language/12-inline-cpp.md) | `cpp { }` passthrough blocks |

### Compiler API (taste)
| Page | Topic |
|---|---|
| [Model](compiler/model.md) | AST node reference |
| [Parser](compiler/parser.md) | `DbParser` — parsing `.db` source |
| [Emitter](compiler/emitter.md) | `Emitter` base class |
| [CppEmitter](compiler/cpp-emitter.md) | C++ code generation |
| [Transpiler](compiler/transpiler.md) | End-to-end pipeline |
| [LanguageMatrix](compiler/language-matrix.md) | Multi-target language profiles |
