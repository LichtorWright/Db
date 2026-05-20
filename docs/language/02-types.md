# Types & Variables

## Primitive Types

Db types map directly to C++ types via the built-in type table.

| Db type | C++ type |
|---|---|
| `int` | `int` |
| `long` | `long long` |
| `float` | `float` |
| `double` | `double` |
| `bool` | `bool` |
| `char` | `char` |
| `string` | `std::string` |
| `void` | `void` |
| `byte` | `uint8_t` |
| `uint` | `unsigned int` |
| `ulong` | `unsigned long long` |

---

## Type Inference

Use `var` to let the compiler infer a local variable's type from its initialiser.

```db
var count = 0;          // int
var name  = "Db";       // std::string
var items = new List<int>();
```

> **Note:** `var` is forbidden in lazy field declarations.  
> See [Lazy Fields](07-lazy.md).

---

## Mutability

| Modifier | Meaning | C++ equivalent |
|---|---|---|
| *(none)* | Mutable by default | plain field / variable |
| `readonly` | Assign once (constructor only) | `const` member |
| `const` | Compile-time constant | `static constexpr` |
| `[Mutable]` | Mutable inside `const` methods | `mutable` |

```db
public readonly int MaxHealth = 100;   // const int MaxHealth = 100;
const int Version = 3;                 // static constexpr int Version = 3;
```

---

## Tuple Types

Return multiple values with a tuple return type.

```db
public (int, string) GetPair() => (42, "hello");
```

Emits as `std::tuple<int, std::string>`.

---

## Generic Types

```db
public List<int> Scores;              // db::List<int> Scores;
public Dictionary<string, int> Map;   // db::Dictionary<std::string, int> Map;
```
