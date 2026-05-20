# Inline C++

When you need to write C++ that has no Db equivalent, use a `cpp { }` passthrough block.  
Lines inside are emitted verbatim — no parsing, no transformation.

---

## Syntax

```db
cpp
{
    #pragma pack(push, 1)
    static_assert(sizeof(Packet) == 8, "Packet size mismatch");
    #pragma pack(pop)
}
```

---

## Use Cases

- Compiler pragmas and `static_assert`
- Preprocessor macros
- Intrinsics or platform-specific code
- Temporary workarounds while a Db feature is in development

---

## Notes

- The `cpp { }` block is a `Statement` — it can appear anywhere a statement is valid (method body, constructor, etc.).
- Indentation inside the block is preserved as-is.
- There is no interaction with the Db type system inside a `cpp { }` block.
- Prefer native Db syntax wherever possible; `cpp { }` is the escape hatch, not the default.
