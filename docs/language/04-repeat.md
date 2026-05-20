# Repeat Loops

`repeat` is Db's dedicated counted-iteration construct.  
It removes the boilerplate of a `for` loop when you just want to run something N times or step through a range.

---

## Anonymous Count

Run a body N times. The hidden counter variable is `_i`.

```db
repeat 10
{
    Tick();
}
```

Emits:

```cpp
for (int _i = 0; _i < 10; ++_i) {
    Tick();
}
```

---

## Named Count

Give the counter a name.

```db
repeat i: 5
{
    Console.WriteLine(i);
}
```

Emits:

```cpp
for (int i = 0; i < 5; ++i) {
    Console.WriteLine(i);
}
```

---

## Forward Range

Iterate from `start` up to (exclusive) `end`.

```db
repeat i: 0 -> 100
{
    Process(i);
}
```

With an explicit step:

```db
repeat i: 0 -> 100 (2)
{
    ProcessEven(i);
}
```

Emits:

```cpp
for (int i = 0; i < 100; i += 2) {
    ProcessEven(i);
}
```

---

## Backward Range

Iterate from `start` down to (exclusive) `end`.

```db
repeat i: 10 <- 0
{
    Console.WriteLine(i);   // 10, 9, 8, … 1
}
```

With a step (applied as subtraction):

```db
repeat i: 100 <- 0 (5)
{
    Countdown(i);            // 100, 95, 90, …
}
```

---

## repeat until

Runs the body while the condition is **false** (equivalent to `while (!(cond))`).

```db
repeat until (IsReady())
{
    Wait();
}
```

Emits:

```cpp
while (!(IsReady())) {
    Wait();
}
```

---

## Loop Guard (postfix)

Wrap the entire `repeat` in a guard condition using a trailing `if` or `unless` on the closing brace line.

```db
repeat 100
{
    Update();
} if (IsActive())
```

Emits:

```cpp
if (IsActive()) {
    for (int _i = 0; _i < 100; ++_i) {
        Update();
    }
}
```

`unless` negates the guard:

```db
repeat i: 0 -> N
{
    Step(i);
} unless (IsPaused())
```
