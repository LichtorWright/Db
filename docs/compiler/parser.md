# DbParser

`taste.Parse.Db.DbParser` converts `.db` source text into a [`CodeFile`](model.md#codefile) AST.  
Source: `taste/Parse/Db/DbParser.cs`

---

## Construction

```csharp
var parser = new DbParser(filePath, sourceLines);
CodeFile file = parser.Parse();
```

`filePath` is used only for error messages and the `CodeFile.Path` property.  
`sourceLines` is the raw source split by line.

---

## Parsing Strategy

The parser is a **line-oriented recursive descent** parser.  
It maintains a cursor (`_pos`) into the line array and advances it as constructs are recognised.

Key helpers:

| Method | Purpose |
|---|---|
| `PeekLine()` | Return current line without consuming it |
| `ConsumeLine()` | Advance cursor and return the consumed line |
| `CollectAttributes()` | Gather `[Attr]` lines preceding a member |
| `ParseAccess(string)` | Map `"public"` / `"private"` / … → `AccessModifier` |

---

## Class Member Parsing Order

Members are detected in this priority order to avoid ambiguity:

1. Constants — `const Type name = expr;`
2. **Lazy fields** — `` `access Type name = expr; ``
3. Fields — `access Type name;`
4. Expression-bodied properties — `access Type name => expr;`
5. Auto-properties — `access Type name { get; set; }`
6. Full-body properties / indexers
7. Events
8. Constructors / destructors
9. Methods

---

## Body Parsing Order

Inside method bodies, statements are detected in this order:

1. Blank lines and comments — skipped
2. `{` opening brace — nested block
3. `cpp {` — inline C++ passthrough
4. `return` — `ReturnStatement` (pipeline applied to expression)
5. `if` / `else` / `unless` — `Condition`
6. `while` / `for` / `foreach` / `do` — `Loop`
7. `repeat` — `Loop (Repeat / RepeatUntil)`
8. `match` — `MatchStatement`
9. `try` — `TryCatchBlock`
10. `using` — `UsingBlock`
11. `lock` — `LockBlock`
12. `throw` — `ThrowStatement`
13. `yield` — `YieldStatement`
14. `checked` / `unchecked` — `CheckedBlock`
15. ` `` ` (double-backtick) — block defer
16. `` ` `` / `defer` — single-line defer
17. `<->` / `swap(…)` — `SwapStatement`
18. `<-` — `MoveStatement`
19. Postfix conditional (` if (` or ` unless (` at depth 0) — `PostfixConditional`
20. **Pipeline transform** applied to RHS of assignment or whole expression
21. Expression statement fallback — `Action`

---

## Pipeline Helpers

| Method | Signature | Description |
|---|---|---|
| `SplitTopLevelCommas` | `(string) → List<string>` | Splits on `,` at paren depth 0 |
| `BuildPipeline` | `(List<string>) → string` | Nests stages right-to-left, injecting prev as first arg |
| `TransformIfPipeline` | `(string) → string` | Applies pipeline iff ≥2 segments with a call |
| `FindTopLevelEquals` | `(string) → int` | Index of first `=` at depth 0 (excluding `==`, `!=`, `<=`, `>=`, `=>`) |

---

## Postfix Conditional Helpers

| Method | Description |
|---|---|
| `TryExtractPostfix` | Scans for ` if (` or ` unless (` at depth 0; extracts primary, condition, optional alt |
| `FindMatchingParen` | Returns the index of the `)` that closes the `(` at a given index |

---

## Error Reporting

Parse errors are thrown as `System.Exception` with a `[Db]` prefix and a human-readable message.  
Example:

```
[Db] Assignment deferrence cannot occur on inferred types.
Replace 'var' with an explicit type for 'result'.
```
