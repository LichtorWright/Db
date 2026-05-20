# Move & Swap

Db exposes C++ move semantics and `std::swap` through dedicated syntax that makes ownership transfer explicit at the call site.

---

## Move Assignment  `<-`

Transfer ownership from a source variable to a target.

```db
// Move into a new declaration
var buffer <- existing;          // auto buffer = std::move(existing);

// Move into an existing variable
result <- computedValue;         // result = std::move(computedValue);
```

Emits:

```cpp
auto buffer = std::move(existing);
result = std::move(computedValue);
```

After the move, the source variable is in a valid-but-unspecified state (standard C++ post-move contract).

---

## Swap  `<->`

Exchange the values of two variables in place.

```db
a <-> b;       // std::swap(a, b);
```

Equivalent functional form (also supported):

```db
swap(a, b);    // std::swap(a, b);
```

---

## Expression-level Helpers

These function names are intercepted by the emitter and rewritten wherever they appear in an expression — including inside method arguments:

| Db expression | C++ output |
|---|---|
| `move(x)` | `std::move(x)` |
| `swap(x, y)` | `std::swap(x, y)` |
| `forward(x)` | `std::forward<decltype(x)>(x)` |

```db
TakeOwnership(move(resource));
Push(forward(arg));
```

---

## Notes

- `<->` is parsed before `<-` to avoid ambiguity.
- `swap(a, b)` as a statement and `<->` are interchangeable; choose whichever reads more clearly.
- `move(x)` in an expression is a hint to the reader that ownership is being transferred, even when inside a larger expression.
