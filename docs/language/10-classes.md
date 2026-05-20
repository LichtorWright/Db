# Classes & Members

Db class declarations follow C# syntax and are emitted as C++ classes or structs.

---

## Basic Class

```db
public class Player
{
    public string Name;
    public int    Health = 100;

    public Player(string name)
    {
        Name = name;
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
    }
}
```

---

## Struct

```db
public struct Vector2
{
    public float X;
    public float Y;
}
```

Add `[Stack]` to emit as a stack-allocated struct with free functions rather than member methods.

---

## Fields

```db
public  int Score;                // public field
private string _name;             // private field
public  readonly int MaxLives;    // const in C++ — assign in constructor only
public  static int InstanceCount; // static field
```

### Lazy Fields

See [Lazy Fields](07-lazy.md).

```db
`public DataTable _rows = Database.Query("SELECT * FROM log");
```

---

## Properties

```db
// Auto-property
public int Score { get; set; }

// Expression-bodied (getter only)
public string Label => $"Score: {Score}";

// Full body
public int Lives
{
    get { return _lives; }
    set { _lives = Math.Max(0, value); }
}
```

---

## Methods

```db
public int Add(int a, int b) => a + b;

public void Reset()
{
    Score = 0;
    Health = 100;
}
```

Modifiers: `static`, `virtual`, `override`, `abstract`, `sealed`, `async`.

---

## Constructors & Destructor

```db
public Player(string name) : _name(name), _health(100)
{
    InstanceCount++;
}

~Player()
{
    InstanceCount--;
}
```

The `: field(expr)` list becomes a C++ member initialiser list.

---

## Events

```db
public event OnDamage DamageTaken;       // multicast — db::List<OnDamage>
public event OnClick  Clicked;
```

Multicast events get `Add`, `Remove`, and `Invoke` helpers emitted automatically.

---

## Operator Overloads

```db
public static Player operator+(Player a, Player b)
{
    return new Player(a.Score + b.Score);
}
```

Emits as a `friend` operator in C++.

---

## Companion Object

A Kotlin-style static container inside the class.

```db
public class Config
{
    companion object
    {
        public const string DefaultPath = "/etc/app.conf";
        public static Config Load() => new Config(DefaultPath);
    }
}
```

Emits as a nested static struct.

---

## Nested Types

```db
public class Outer
{
    public class Inner { }
    public enum State { Active, Idle }
}
```

---

## Interfaces

```db
public interface IDrawable
{
    void Draw();
    int  ZOrder { get; }
}
```

Emits as an abstract base class with pure virtual methods.

---

## Generics

```db
public class Box<T>
{
    public T Value;
    public Box(T v) { Value = v; }
}
```

Emits as a C++ template class.

---

## Multiple Inheritance & Composition (`with`)

Db supports composing new object instances from existing base instances using the `with` operator. This seamlessly bridges C# object initializer syntax with C++ multiple inheritance constructors.

```db
public class ClassC : ClassA, ClassB { ... }

// Merge existing base instances to instantiate a derived class
ClassC example = base1 with base2;

// The left-hand side can be an inline instantiation featuring named arguments
ClassC composite = new ClassA(Health: 100) with new ClassB(Speed: 5);
```

This transpiles to C++ uniform brace initialization `{ base1, base2 }`, which perfectly resolves to the implicit or explicit copying/moving inheritance constructors of the composite type.
