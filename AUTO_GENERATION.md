# Auto-Generation System

## Overview

The transpiler now includes automatic file generation for both directions:
1. **C++ Headers → C# Stubs** (`.h` → `.db`)
2. **C# Db Files → C++ Generated** (`.db` → `.g.cpp`)

## File Watchers

### 1. HeaderWatcher (C++ → C#)

**Purpose**: Automatically generate C# IntelliSense stubs from C++ headers

**Folders**:
- `Headers/` - Place C++ `.h` files here
- `Stubs/` - Generated C# `.db` stub files

**Triggers**:
- ✅ File Created → Generate stub
- ✅ File Changed → Regenerate stub
- ✅ File Deleted → Delete stub

**Example**:
```cpp
// Headers/Character.h
class Character {
    int health;
    int GetHealth() const;
};
```

↓ Auto-generates ↓

```csharp
// Stubs/Character.db
public class Character {
    public int Health { get; set; }
    public int GetHealth();
}
```

### 2. DbWatcher (C# → C++)

**Purpose**: Automatically generate C++ code from C# Db files on save

**Folders**:
- Root workspace - Place `.db` files here
- `Generated/` - Generated `.g.cpp` files

**Triggers**:
- ✅ File Created → Generate `.g.cpp`
- ✅ File Changed → Regenerate `.g.cpp` (on save!)
- ✅ File Deleted → Delete `.g.cpp`

**Example**:
```csharp
// Player.db
public class Player {
    public int Health { get; set; }
    public void Attack(Player target);
}
```

↓ Auto-generates ↓

```cpp
// Generated/Player.g.cpp
#include "dbuild.h"
using namespace db;

public class Player {
public int health;
public int getHealth() const { return health; }
public void setHealth(int value) { health = value; }
public void Attack(Player target);
}
```

## Usage

### Start Both Watchers

```bash
dotnet run --project DbTranspiler
```

This starts both watchers and keeps them running in the background.

### Manual Transpilation

```bash
# C# to C++
dotnet run --project DbTranspiler -- input.db output.cpp

# C++ to C# (reverse)
# Handled automatically by HeaderWatcher
```

## File Naming Convention

### Generated Files Use `.g.cpp` Suffix

- `.g.cpp` = **G**enerated C++ file
- Distinguishes auto-generated code from hand-written code
- Can be added to `.gitignore` if desired
- Similar to `.g.cs` (generated C#) convention

### Stub Files

- `.db` in `Stubs/` folder
- Auto-generated from C++ headers
- Provides IntelliSense for C++ types in C#

## Workflow

### Low-Level First (C++ → C#)

1. Write C++ header in `Headers/`
2. Stub auto-generated in `Stubs/`
3. Use stub for IntelliSense in C#
4. Write C# code using stub types

### High-Level to Low-Level (C# → C++)

1. Write C# code in `.db` file
2. Code auto-generates to `Generated/*.g.cpp`
3. Compile generated C++ with your build system
4. Link with hand-written C++ code

## Benefits

### Real-Time Feedback

- Save `.db` file → See C++ output immediately
- Catch transpilation errors early
- Iterate faster

### IntelliSense Support

- C++ headers → C# stubs → Full IntelliSense
- No manual type definitions needed
- Stay in sync with C++ codebase

### Clean Separation

- `Headers/` - Hand-written C++
- `Stubs/` - Auto-generated C# (from C++)
- `*.db` - Hand-written C# (Db language)
- `Generated/` - Auto-generated C++ (from C#)

## Configuration

### Watch Folders

Default folders (relative to workspace root):
- `Headers/` - C++ headers
- `Stubs/` - C# stubs from headers
- `Generated/` - C++ from Db files

### Filtering

- Only `*.h` files trigger stub generation
- Only `*.db` files trigger C++ generation
- Files in `Stubs/` folder are ignored (prevent loops)

## Error Handling

### Transpilation Errors

Errors are logged to console but don't stop the watcher:

```
[DbWatcher] ✗ [E005] in Player.db: Unknown type 'Vector2' cannot be mapped to C++
```

Fix the error in the `.db` file and save → auto-regenerates.

### File System Errors

Missing folders are created automatically.

## Performance

### Debouncing

File changes are debounced to prevent rapid regeneration:
- Header changes: 100ms delay
- Db file changes: 200ms delay

### Background Processing

Watchers run in background, non-blocking.

## Future Enhancements

### Watcher Improvements

- [ ] Configurable watch folders via settings
- [ ] Batch regeneration option
- [ ] Progress indicators for large files
- [ ] Watch nested subdirectories

### Generation Improvements

- [ ] Incremental generation (only changed classes)
- [ ] Parallel processing for multiple files
- [ ] Custom output naming patterns
- [ ] Pre/post-generation hooks

## Troubleshooting

### Files Not Generating

1. Check watcher is running (look for `[DbWatcher]` messages)
2. Verify file extension (`.db` or `.h`)
3. Check folder structure matches expected layout
4. Look for error messages in console

### Stale Generated Files

1. Delete `Generated/` folder
2. Restart watcher
3. All files will regenerate

### IntelliSense Not Working

1. Ensure stub file exists in `Stubs/`
2. Check C# project includes `Stubs/` folder
3. Reload C# project if needed
