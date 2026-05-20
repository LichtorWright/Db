# AST Model Reference

The `taste` namespace defines the full Abstract Syntax Tree used by the parser and emitter.  
All types live in `taste/Model.cs`.

---

## Enumerations

### `AccessModifier`
Controls C++ access specifier emission.

| Value | C++ |
|---|---|
| `Public` | `public:` |
| `Private` | `private:` |
| `Protected` | `protected:` |
| `Internal` | `public:` *(no module system in C++)* |

---

### `ConditionKind`
Kind of a [`Condition`](#condition) statement.

| Value | Meaning |
|---|---|
| `If` | `if (expr)` |
| `Unless` | `if (!(expr))` |
| `Else` | `else` |
| `ElseIf` | `else if (expr)` |
| `Switch` | `switch (expr)` |
| `Inline` | Ternary `? :` |
| `Match` | Pattern-matching `match` |

---

### `LoopKind`
Kind of a [`Loop`](#loop) statement.

| Value | Emitted as |
|---|---|
| `While` | `while (expr)` |
| `DoWhile` | `do { } while (expr);` |
| `For` | `for (init; cond; step)` |
| `ForEach` | Range-based `for (auto& x : coll)` |
| `Repeat` | `for (int _i = 0; _i < N; ++_i)` or directional range |
| `RepeatUntil` | `while (!(cond))` |

---

### `MutabilityModifier` *(flags)*

| Flag | Meaning |
|---|---|
| `Const` | C++ `const` / C# `readonly` |
| `Mutable` | C++ `mutable` |
| `Volatile` | C++ `volatile` |
| `ReadOnly` | C# `readonly` (constructor-only assignment) |

---

### `MemberAccess`
How a stub member is accessed in C++ (used by `[Represents]`).

`Dot` · `Arrow` · `Colon` · `Colons` · `DotAsterisk` · `ArrowAsterisk` · `QuestionMarkDot` · `QuestionMarkBracket` · `Bracket` · `None`

---

### `TypeDecoration`
Type decoration applied by `[Address]`, `[Reference]`, or `[Naked]`.

| Value | C++ |
|---|---|
| `None` | plain `T` |
| `Address` | `T*` |
| `Reference` | `T&` |
| `Naked` | `T` (strips default decoration) |

---

### `MethodModifier` *(flags)*

`None` · `Static` · `Virtual` · `Abstract` · `Override` · `Sealed` · `Async` · `Extern` · `New`

---

## Top-level Containers

### `CodeFile`
The root node produced by `DbParser`. Represents one `.db` source file.

| Property | Type | Description |
|---|---|---|
| `Path` | `string` | Source file path |
| `Usings` | `List<Using>` | `using` directives |
| `Namespaces` | `List<Namespace>` | Top-level namespaces |
| `Classes` | `List<Class>` | File-scope classes |
| `Interfaces` | `List<InterfaceDeclaration>` | File-scope interfaces |
| `SumTypes` | `List<SumTypeDeclaration>` | Discriminated unions |
| `Mixins` | `List<MixinDeclaration>` | Extension methods / impl blocks |
| `TypeAliases` | `List<TypeAliasDeclaration>` | `type Alias = T;` |
| `Enums` | `List<EnumDecl>` | Enum declarations |
| `Delegates` | `List<DelegateDecl>` | Delegate type declarations |
| `FileScopeDirectives` | `List<FileScopeDirective>` | `#include`, `mod`, etc. |

---

### `FileScopeDirective`
A file-scope directive such as a C++ `#include`.

| Property | Type | Description |
|---|---|---|
| `Kind` | `string` | `"include"`, `"mod"`, `"using"`, … |
| `Target` | `string` | Header name or module name |
| `IsSystem` | `bool` | `true` → `<header>`, `false` → `"header"` |

---

## Type Declarations

### `Class`
A class or struct declaration.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | |
| `IsStruct` | `bool` | `struct` vs `class` |
| `Allocation` | `AllocationStrategy` | `Stack`, `Unique`, `Shared`, `Raw`, or `Default` |
| `IsAbstract` | `bool` | |
| `IsSealed` | `bool` | |
| `IsStatic` | `bool` | |
| `BaseClasses` | `List<BaseClass>` | Inheritance list |
| `TypeParams` | `List<string>` | Generic type parameters |
| `Constants` | `List<Constant>` | `const` members |
| `Fields` | `List<Field>` | Plain member variables |
| `Properties` | `List<Property>` | Auto-properties |
| `Indexers` | `List<Indexer>` | `this[…]` indexers |
| `Operators` | `List<OperatorOverload>` | Operator overloads |
| `Events` | `List<Event>` | Event members |
| `Methods` | `List<Method>` | |
| `Finalizer` | `Method?` | `~ClassName()` |
| `Companion` | `CompanionObject` | Kotlin-style companion |
| `NestedClasses` | `List<Class>` | |
| `NestedEnums` | `List<EnumDecl>` | |
| `FriendClasses` | `List<string>` | `friend class X;` |
| `Attributes` | `List<DbAttribute>` | |

---

### `Field`
A plain member variable.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | |
| `Type` | `string` | Db type name |
| `Access` | `AccessModifier` | |
| `Initializer` | `string?` | RHS expression |
| `IsStatic` | `bool` | |
| `IsReadonly` | `bool` | `const` in C++ |
| `IsLazy` | `bool` | Backtick lazy initialisation |
| `Mutability` | `MutabilityModifier` | |
| `Attributes` | `List<DbAttribute>` | |

---

### `Property`
A C# auto-property or expression-bodied property.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | |
| `Type` | `string` | |
| `HasGetter` | `bool` | |
| `HasSetter` | `bool` | |
| `IsExpressionBodied` | `bool` | `=> expr` form |
| `Initializer` | `string?` | Expression after `=>` |
| `IsIndexer` | `bool` | `this[…]` |
| `IndexerParameters` | `List<Parameter>` | |
| `IsVirtual` / `IsOverride` | `bool` | |
| `Mutability` | `MutabilityModifier` | |

---

### `Method`

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | |
| `ReturnType` | `string` | |
| `IsConstructor` | `bool` | |
| `IsDestructor` | `bool` | |
| `Modifiers` | `MethodModifier` | Combined flags |
| `TypeParams` | `List<string>` | Generics |
| `Parameters` | `List<Parameter>` | |
| `CtorInitializers` | `List<CtorInitializer>` | `: field(expr)` list |
| `Body` | `List<Statement>` | |
| `Mutability` | `MutabilityModifier` | e.g. `const` method |

---

### `Constant`
`const int Max = 100;` → `static constexpr int Max = 100;`

### `Event`
`public event OnDamage Fired;` — multicast or singlecast.

### `DelegateDecl`
`public delegate void OnClick(int x);` → `using OnClick = std::function<void(int)>;`

### `TypeAliasDeclaration`
`public type Alias = Original;` → `using Alias = Original;`

### `SumTypeDeclaration`
Discriminated union — emits as `std::variant`.

### `MixinDeclaration`
Extension / `impl` block — adds methods to an existing type.

---

## Statements

All statement types inherit from the abstract `Statement` class.

| Type | Description |
|---|---|
| `Action` | Assignment, call, or other expression statement |
| `Condition` | `if` / `unless` / `else` / `switch` / `match` |
| `Loop` | `while` / `for` / `foreach` / `repeat` / `repeat until` |
| `DoWhileStatement` | `do { } while (cond);` |
| `ReturnStatement` | `return expr;` |
| `BreakStatement` | `break;` |
| `ContinueStatement` | `continue;` |
| `DeferStatement` | RAII scope-guard defer |
| `MoveStatement` | `<-` move assignment |
| `SwapStatement` | `<->` / `swap(a,b)` |
| `PostfixConditional` | Trailing `if` / `unless` |
| `TryCatchBlock` | `try / catch / finally` |
| `UsingBlock` | `using (var r = …) { }` |
| `LockBlock` | `lock (obj) { }` |
| `ThrowStatement` | `throw new Ex(…);` |
| `YieldStatement` | `yield return` / `yield break` |
| `CheckedBlock` | `checked { }` / `unchecked { }` |
| `BlockStatement` | Explicit `{ }` scope |
| `MatchStatement` | Pattern-matching arms |
| `InlineCppBlock` | `cpp { }` verbatim passthrough |

### `DeferStatement`
```
Body: string   — the statement text to execute on scope exit
```

### `MoveStatement`
```
Target:        string
Source:        string
IsDeclaration: bool   — true when declaring a new variable (var b <- a)
```

### `SwapStatement`
```
Left:  string
Right: string
```

### `PostfixConditional`
```
Primary:   string   — action when condition holds
Condition: string
IsUnless:  bool     — negate the condition
Alt:       string?  — optional else branch
```
