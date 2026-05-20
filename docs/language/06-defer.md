# Defer

Deferred statements run automatically at the **end of the enclosing scope** regardless of how control exits — normal return, early return, or exception.  
The implementation uses a C++17 RAII scope guard; no heap allocation is involved.

---

## Single-line Defer

Prefix a statement with a backtick (`` ` ``) or the `defer` keyword.

```db
void OpenFile(string path)
{
    var f = File.Open(path);
    `f.Close();          // deferred — runs when the function exits

    Process(f);
}
```

Equivalent form using the keyword:

```db
defer f.Close();
```

Emits (C++17 scope guard):

```cpp
auto _defer_0 = [&]{ f.Close(); };
struct _Defer_0 { decltype(_defer_0)& fn; ~_Defer_0(){ fn(); } } _guard_0{_defer_0};
```

---

## Block Defer

Use double-backtick (` `` `) to defer multiple lines as a block.

````db
``
    Log("cleanup start");
    Flush();
    Log("cleanup done");
``
````

The opening ` `` ` must be alone on its line; the closing ` `` ` ends the block.  
All collected lines are wrapped in a single RAII scope guard.

---

## LIFO Ordering

Multiple defers in the same scope execute in **last-in, first-out** order — the reverse of their declaration order.

```db
`Log("first declared");    // runs second
`Log("second declared");   // runs first
```

---

## Built-in Move Helpers

Inside any expression (not just defer), these calls are rewritten at emit time:

| Db call | C++ output |
|---|---|
| `move(x)` | `std::move(x)` |
| `swap(x, y)` | `std::swap(x, y)` |
| `forward(x)` | `std::forward<decltype(x)>(x)` |
