# Lazy Fields

A lazy field is initialised **on first access** and cached for all subsequent reads.  
It is the class-member equivalent of the classic null-check pattern — without the boilerplate.

---

## Syntax

Place a backtick (`` ` ``) before the access modifier:

```db
`public  SomeType fieldName = InitialiserExpression();
`private SomeType fieldName = InitialiserExpression();
```

The access modifier and initialiser are both required.  
`var` (type inference) is **not** permitted — the type must be explicit.

---

## Example

```db
class Report
{
    `private DataTable _rows = Database.Query("SELECT * FROM log");
    `public  string    Title = ComputeTitle();
}
```

Emits (once per file — the `db_lazy_field` helper template):

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

Then per field:

```cpp
db_lazy_field _rows{[this]{ return Database.Query("SELECT * FROM log"); }};
db_lazy_field Title{[this]{ return ComputeTitle(); }};
```

---

## Semantics

- The factory lambda captures `this` so it can call instance methods.
- The value is computed **once** — on the first access — then stored in a `std::optional`.
- `operator value_type&()` makes the field transparent in arithmetic, function calls, and assignments without any special syntax.
- Thread safety is **not** guaranteed by default; add a mutex if concurrent access is expected.

---

## Error: Inferred Type

Using `var` in a lazy declaration is a compile-time error:

```db
`public var result = Compute();   // ✗
```

> **[Db] Assignment deferrence cannot occur on inferred types.**  
> Replace `var` with an explicit type for `result`.
