# Emitter (base class)

`taste.Emit.Emitter` is the abstract base class for all code generators.  
Source: `taste/Emit/Emitter.cs`

---

## Responsibilities

- Owns the `StringBuilder` output buffer and indent level.
- Provides `WriteLine`, `Indent`, `Dedent` helpers used by all subclasses.
- Dispatches `WriteStatement` to the correct abstract method based on the concrete `Statement` type.
- Declares abstract `Write*` methods that each target emitter must implement.

---

## Output Helpers

| Method | Description |
|---|---|
| `WriteLine(string)` | Emit a line at the current indent level |
| `WriteLine()` | Emit a blank line |
| `Indent()` | Increase indent by one level |
| `Dedent()` | Decrease indent by one level |
| `GetOutput()` | Return the accumulated source text |

---

## Statement Dispatch

`WriteStatement(Statement)` switches on the concrete type and calls the matching abstract method:

| Statement type | Abstract method |
|---|---|
| `Action` | `WriteAction` |
| `Condition` | `WriteCondition` |
| `Loop` | `WriteLoop` |
| `DoWhileStatement` | `WriteDoWhile` |
| `ReturnStatement` | `WriteReturn` |
| `BreakStatement` | `WriteBreak` |
| `ContinueStatement` | `WriteContinue` |
| `DeferStatement` | `WriteDefer` |
| `MoveStatement` | `WriteMove` |
| `SwapStatement` | `WriteSwap` |
| `PostfixConditional` | `WritePostfixConditional` |
| `TryCatchBlock` | `WriteTryCatch` |
| `UsingBlock` | `WriteUsing` |
| `LockBlock` | `WriteLock` |
| `ThrowStatement` | `WriteThrow` |
| `YieldStatement` | `WriteYield` |
| `CheckedBlock` | `WriteChecked` |
| `BlockStatement` | `WriteBlock` |
| `MatchStatement` | `WriteMatch` |
| `InlineCppBlock` | `WriteInlineCpp` |

---

## Abstract Interface (partial)

```csharp
protected abstract void WriteFileHeader(CodeFile file);
protected abstract void WriteNamespace(Namespace ns);
protected abstract void WriteClass(Class cls);
protected abstract void WriteField(Field field);
protected abstract void WriteProperty(Property prop);
protected abstract void WriteMethod(Method method);
protected abstract void WriteCondition(Condition stmt);
protected abstract void WriteLoop(Loop stmt);
protected abstract void WriteDefer(DeferStatement stmt);
protected abstract void WriteMove(MoveStatement stmt);
protected abstract void WriteSwap(SwapStatement stmt);
protected abstract void WritePostfixConditional(PostfixConditional stmt);
// … and more
```

---

## Language Profile

Each emitter receives a `LanguageProfile` from the `LanguageMatrix`.  
The profile provides keyword mappings (`if`, `while`, `class`, `namespace`, …) and access modifier strings so that the same emitter logic can target multiple output languages.
