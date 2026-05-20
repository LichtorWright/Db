# Attributes

Db attributes control how the emitter generates C++ output.  
Most have no runtime cost — they are instructions to the compiler.

---

## Allocation

| Attribute | Effect |
|---|---|
| `[Stack]` | Stack-allocated value type. Destructor runs at scope exit. Zero overhead. |
| `[Heap]` | `std::unique_ptr` — sole ownership, deterministic destruction. No reference count. |
| `[Shared]` | `std::shared_ptr` — shared ownership, reference-counted. Last reference cleans up. |
| `[Raw]` | Raw pointer — manual management. Compiler enforces explicit cleanup in finalizer. |

```db
[Stack]
public struct Vec3
{
    public float X, Y, Z;
}

[Heap]
private Logger Log;           // unique_ptr — sole ownership

[Shared]
public Player Hero;            // shared_ptr — others can reference

[Raw]
private byte* Buffer;          // raw pointer — must be freed in finalizer
```

### Memory Management Profile

Per-class override of the project-level memory policy:

```db
[MemoryManagement(Public=Shared, Private=Unique, Local=Stack)]
public class Game
{
    public Player Hero;           // shared_ptr (from class policy)
    private Logger Log;           // unique_ptr (from class policy)
    
    public void Start()
    {
        var config = new Config(); // stack (from class policy)
    }
}
```

Resolution chain: **member attribute → class [MemoryManagement] → project XML config → RAII default**.

---

## Type Decoration

Applied to fields, parameters, or return types to control pointer/reference decoration.

| Attribute | C++ decoration |
|---|---|
| `[Address]` | `T*` |
| `[Reference]` | `T&` |
| `[Naked]` | `T` (strips any default decoration) |

```db
[Address]   public Node Next;     // Node* Next;
[Reference] public int  Value;    // int&  Value;
```

---

## Member Modifiers

| Attribute | Effect |
|---|---|
| `[Const]` | Marks a method as `const` in C++ |
| `[Mutable]` | Marks a field as `mutable` (changeable in const methods) |
| `[Volatile]` | Adds `volatile` qualifier |
| `[Noexcept]` | Marks a method `noexcept` |
| `[Explicit]` | Marks a constructor `explicit` |

```db
[Const]
public int GetHealth() => _health;

[Mutable]
private int _cacheVersion;
```

---

## `[Represents]`

Maps a Db stub member to a specific C++ access pattern.  
Used in stub files to describe how an existing C++ API is accessed.

```db
[Represents("size()", MemberAccess.Dot)]
public int Length { get; }
```

Access modes:

| Value | C++ operator |
|---|---|
| `Dot` | `.` |
| `Arrow` | `->` |
| `Colons` | `::` |
| `Bracket` | `[]` |
| `None` | Free function / constructor |

---

## `[Friend]`

Declares a C++ `friend class`.

```db
[Friend(PhysicsEngine)]
public class RigidBody { }
```

Emits:

```cpp
class RigidBody {
    friend class PhysicsEngine;
    ...
};
```

---

## `[Include]` / `[SystemInclude]`

Adds a `#include` directive to the generated file.

```db
[Include("my_header.h")]
[SystemInclude("vector")]
public stub MyType { }
```
