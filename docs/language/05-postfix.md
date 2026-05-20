# Postfix Conditionals

A postfix conditional attaches an `if` or `unless` clause to the **end** of a statement.  
This keeps the happy-path prominent and avoids extra braces for simple guards.

---

## Syntax

```
primary if (condition);
primary if (condition) : alternative;

primary unless (condition);
primary unless (condition) : alternative;
```

---

## Examples

### Simple guard

```db
return null if (count == 0);
```

Emits:

```cpp
if (count == 0) { return null; }
```

### With alternative

```db
return cached if (cached != null) : Recompute();
```

Emits:

```cpp
if (cached != null) { return cached; } else { Recompute(); }
```

### unless

```db
Log("connected") unless (IsSilent());
```

Emits:

```cpp
if (!(IsSilent())) { Log("connected"); }
```

---

## Notes

- Works with any statement: `return`, method calls, assignments, etc.
- The `: alternative` branch is optional.
- The condition must be parenthesised.
- Postfix conditionals are detected at parse time by scanning for ` if (` or ` unless (` at depth 0.
