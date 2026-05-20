# Control Flow

## if / else if / else

Standard C#-style conditionals.

```db
if (health <= 0)
{
    Die();
}
else if (health < 20)
{
    PlayWarnSound();
}
else
{
    Heal(5);
}
```

---

## unless

`unless (condition)` is shorthand for `if (!(condition))`.  
Use it when the negative case is the interesting one.

```db
unless (IsConnected())
{
    Connect();
}
```

Emits as:

```cpp
if (!(IsConnected())) {
    Connect();
}
```

---

## match

Pattern-matching switch with guard conditions.

```db
match (shape)
{
    Circle c  => DrawCircle(c.Radius);
    Rect r if (r.Width == r.Height) => DrawSquare(r.Width);
    Rect r    => DrawRect(r.Width, r.Height);
    _         => throw new ArgumentException("Unknown shape");
}
```

---

## while

```db
while (queue.Count > 0)
{
    Process(queue.Dequeue());
}
```

---

## for

```db
for (int i = 0; i < 10; i++)
{
    Console.WriteLine(i);
}
```

---

## foreach

```db
foreach (var item in collection)
{
    Process(item);
}
```

Emits as a range-based for loop:

```cpp
for (auto& item : collection) {
    Process(item);
}
```

---

## do-while

```db
do
{
    ReadInput();
} while (!IsValid());
```

---

## try / catch / finally

```db
try
{
    Load(path);
}
catch (IOException ex)
{
    Log(ex.Message);
}
finally
{
    Cleanup();
}
```

Optional `when` filters:

```db
catch (Exception ex) when (ex.Code == 42)
{
    HandleSpecific();
}
```

---

## lock

```db
lock (_mutex)
{
    _counter++;
}
```

Emits as:

```cpp
{
    std::lock_guard<std::mutex> _lock(_mutex);
    _counter++;
}
```
