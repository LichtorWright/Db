# Pipeline Operator

The pipeline operator lets you compose a chain of function calls left-to-right using commas.  
The result of each stage is injected as the **first argument** of the next stage.

---

## Syntax

```
seed, Stage1(), Stage2(extraArg), Stage3()
```

is equivalent to:

```
Stage3(Stage2(Stage1(seed), extraArg))
```

---

## Examples

### Standalone statement

```db
rawInput, Trim(), ToLower(), Hash();
```

Becomes:

```cpp
Hash(ToLower(Trim(rawInput)));
```

### Assignment

```db
string result = raw, Trim(), Escape(), Encode();
```

Becomes:

```cpp
string result = Encode(Escape(Trim(raw)));
```

### With extra arguments

Extra arguments in a stage are preserved and shift to the right:

```db
items, Filter(x => x > 0), Sort(), First();
```

Becomes:

```cpp
First(Sort(Filter(items, x => x > 0)));
```

### Return statement

```db
return input, Validate(), Normalise(), Hash();
```

Becomes:

```cpp
return Hash(Normalise(Validate(input)));
```

---

## Rules

1. The first segment is the **seed value** — it can be any expression (variable, literal, call).
2. Subsequent segments must be **function calls** (contain `(`). A segment without `(` is treated as a function name: `name` → `name(prev)`.
3. Commas inside parentheses are **not** treated as pipeline separators — depth tracking is used.
4. Pipeline is applied to **return expressions** and the **right-hand side of assignments**.
5. If there is only one segment, or no segment is a call, the expression is left unchanged.

---

## Comparison

| Style | Code |
|---|---|
| Nested calls | `Hash(Normalise(Validate(input)))` |
| Pipeline | `input, Validate(), Normalise(), Hash()` |

The pipeline form reads left-to-right in execution order, matching how the data flows.
