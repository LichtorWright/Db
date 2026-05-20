# LanguageMatrix

`taste.LanguageMatrix` defines per-target language profiles used by the emitter base class.  
Source: `taste/LanguageMatrix.cs`

---

## Purpose

The `LanguageMatrix` maps `LanguageName` values to `LanguageProfile` objects.  
Each profile contains keyword strings and access modifier tokens for one output language, allowing the same emitter logic to be reused across targets.

---

## Supported Languages

| `LanguageName` | Description |
|---|---|
| `CPlusPlus` | C++17/20 — primary target |

Additional targets (Rust, Swift, …) can be added by extending the matrix and implementing a new `Emitter` subclass.

---

## `LanguageProfile` Properties

| Property | Example (C++) |
|---|---|
| `Namespace` | `"namespace"` |
| `Class` | `"class"` |
| `Struct` | `"struct"` |
| `If` | `"if"` |
| `Else` | `"else"` |
| `While` | `"while"` |
| `For` | `"for"` |
| `Return` | `"return"` |
| `AccessPublic` | `"public"` |
| `AccessPrivate` | `"private"` |
| `AccessProtected` | `"protected"` |

---

## `KeywordFor`

The emitter calls `KeywordFor(CodePart part)` to retrieve the correct keyword for the active profile:

```csharp
WriteLine($"{KeywordFor(CodePart.Namespace)} {ns.Name} {{");
// → "namespace MyApp {"
```

This makes emitter code independent of the specific output language's keyword choices.
