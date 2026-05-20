# CppEmitter

`taste.Emit.CppEmitter` extends `Emitter` and generates C++17/20 source.  
Source: `taste/Emit/CPP/CppEmitter.cs`

---

## Type Mapping

Built-in Db → C++ type conversions are defined in `CppTypes.TypeMap`.

Tuple types `(A, B)` → `std::tuple<A, B>`.  
Unknown types pass through unchanged (enabling use of native C++ type names).

---

## Type Decoration

`[Address]` → `T*`  `[Reference]` → `T&`  `[Naked]` → `T`

Applied to fields, parameters, and return types via `GetTypeDecoration` + `DecorateType`.

---

## Class Emission

Members are emitted in access-specifier groups:

```
private:
  friend declarations
  constants
  plain fields
  property backing fields
  event backing lists

protected:
  fields

public:
  constants
  fields
  properties
  events
  methods
  operator overloads

private:
  private methods
```

---

## Field Emission

### Regular Field

```cpp
[static] [const] [mutable] Type name[ = init];
```

### Lazy Field (`IsLazy = true`)

Emits the `db_lazy_field` template once per file (guarded by `_lazyTemplateEmitted`), then:

```cpp
db_lazy_field name{[this]{ return InitialiserExpression(); }};
```

The template uses CTAD to deduce `T` from the factory lambda's return type.

```cpp
template<typename F>
struct db_lazy_field {
    using value_type = std::invoke_result_t<F>;
    mutable std::optional<value_type> _v;
    F _factory;
    explicit db_lazy_field(F f) : _factory(std::move(f)) {}
    value_type& get()          const { if (!_v) _v = _factory(); return *_v; }
    operator value_type&()     const { return get(); }
    value_type* operator->()   const { return &get(); }
    value_type& operator*()    const { return get(); }
};
template<typename F> db_lazy_field(F) -> db_lazy_field<std::decay_t<F>>;
```

---

## Defer Emission

`DeferStatement` → C++17 RAII scope guard (no heap allocation):

```cpp
auto _defer_N = [&]{ body; };
struct _Defer_N { decltype(_defer_N)& fn; ~_Defer_N(){ fn(); } } _guard_N{_defer_N};
```

`_deferCounter` is incremented per defer to ensure unique names.  
Multi-line block defer bodies are emitted with `Indent`/`Dedent`.

---

## Move & Swap Emission

| Db | C++ |
|---|---|
| `var b <- a;` | `auto b = std::move(a);` |
| `b <- a;` | `b = std::move(a);` |
| `a <-> b;` | `std::swap(a, b);` |

Expression-level intercepts in `EmitInvocation`:

| Db call | C++ output |
|---|---|
| `move(x)` | `std::move(x)` |
| `swap(x, y)` | `std::swap(x, y)` |
| `forward(x)` | `std::forward<decltype(x)>(x)` |

---

## Condition Emission

| `ConditionKind` | C++ |
|---|---|
| `If` | `if (expr)` |
| `Unless` | `if (!(expr))` |
| `Else` | `else` |
| `ElseIf` | `else if (expr)` |
| `Switch` | `switch (expr)` |
| `Match` | Series of `if / else if` with pattern guards |

---

## Loop Emission

| `LoopKind` | C++ |
|---|---|
| `While` | `while (expr)` |
| `For` | `for (init; cond; step)` |
| `ForEach` | `for (auto& var : collection)` |
| `Repeat` (count) | `for (int _i = 0; _i < N; ++_i)` |
| `Repeat` (named count) | `for (int i = 0; i < N; ++i)` |
| `Repeat` (forward range `->`) | `for (int i = start; i < end; i += step)` |
| `Repeat` (backward range `<-`) | `for (int i = start; i > end; i -= step)` |
| `RepeatUntil` | `while (!(cond))` |

---

## Postfix Conditional Emission

```cpp
// primary if (cond)
if (cond) { primary; }

// primary if (cond) : alt
if (cond) { primary; } else { alt; }

// primary unless (cond)
if (!(cond)) { primary; }
```

---

## Property Emission

- **Auto-property**: backing field `_name` + `name()` getter + `set_name(v)` setter.
- **Expression-bodied**: inline getter only.
- **Indexer**: `T& operator[](params)` and optionally a const overload.

---

## Constructor Emission

C++ member initialiser list is emitted from `CtorInitializers`:

```cpp
Player::Player(const std::string& name)
    : _name(name), _health(100)
{ ... }
```

---

## Operator Overloads

Emitted as `friend` operators inside the class body.

---

## Events

Multicast events emit:
- `db::List<DelegateType> _eventName;` (private backing)
- `void add_EventName(DelegateType h)` — adds handler
- `void remove_EventName(DelegateType h)` — removes handler
- `void invoke_EventName(args…)` — calls all handlers
