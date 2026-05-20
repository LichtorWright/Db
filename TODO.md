# taste TODO

## ✅ Completed
- [x] Access modifier enforcement on classes and members
- [x] Class vs Struct semantics (heap `shared_ptr` vs stack direct instance)
- [x] Method declarations with return types and parameters
- [x] Constructor parameters (`new Player(100, "Hero")`)
- [x] Generic type conversion (`List<int>` → `std::vector<int>`, `Dictionary<K,V>` → `std::unordered_map<K,V>`, etc.)
- [x] Member access operator (`.` vs `->`) based on class/struct and `[Stack]`/`[Heap]` attributes
- [x] Return statements
- [x] Type mapping (built-in types, custom runtime types)
- [x] Property declarations (`get; set;`) → C++ backing field + getter/setter methods
- [x] Collection types: `List`, `Dictionary`, `HashSet`, `Queue`, `Stack`
- [x] LINQ → C++20 Ranges (`Where`, `Select`, `OrderBy`, `Take`, `Skip`, `ToList`, etc.)
- [x] Error handling with structured error codes (E001–E006)
- [x] `unsafe` block stripping
- [x] Multiple inheritance via `I`-prefix convention (`IFoo` → `Foo`)
- [x] `[Stack]` / `[Heap]` allocation override attributes
- [x] `[Friend(ClassName)]` attribute → C++ `friend class`
- [x] `foreach` → range-based `for` loop
- [x] `.db` file watcher → auto-generates `.g.cpp` on save (`DbFileWatcher.cs`)
- [x] Reverse transpiler: `.h` → `.db` stubs (`CppHeaderToDbStub.cs`)
- [x] Header folder watcher: `.h` → auto-generates stubs (`HeaderFolderWatcher.cs`)
- [x] C++ access specifier block labels (`public:`) instead of per-member modifiers
- [x] Class/struct closing brace semicolon (`};`)
- [x] Setter `this->field` disambiguation (fixes `value = value` self-assignment)
- [x] `Dbuild.IntelliSense` project: C# aliases for C++ types (IntelliSense support)
- [x] `Runtime/` C++ headers: `Object.h`, `String.h`, `Int32.h`, `Boolean.h`, `Double.h`
- [x] `CMakeLists.txt`: globs `Generated/*.g.cpp` + `Source/*.cpp` for build
- [x] `CppTypes.cs`: centralised C++ type mappings in `taste.C` namespace
- [x] Project renamed: DbTranspiler → taste
- [x] ExpressionParser: full recursive descent parser (17 precedence levels, all 15 Expression AST types)
- [x] ExpressionParser wired into DbParser (return statements, variable initializers, method bodies)
- [x] Method body parsing (was previously skipped entirely)
- [x] DbResult base class concept (opt-in structured error reporting, implicit unwrap/upcast)
- [x] ConditionKind/LoopKind split — loops are now `Loop` nodes with `LoopKind`, conditions are `Condition` with `ConditionKind`
- [x] IfElse → ElseIf rename

## ⏳ In Progress / Blocked

### CMake Build System (`CppBuildSystem.cs`)
- Generator name `"Visual Studio 17 2022"` is split on spaces when passed to `Process.Start`
- CMake 4.3 only has Visual Studio generators on this machine (no Ninja, no NMake)
- Need to fix argument quoting or switch to a different invocation strategy

## 📋 Remaining Work

### Must Have
- [x] `.g.h` header file generation (alongside `.g.cpp`)
- [x] Constructor detection and correct C++ output
- [x] Parameter type mapping (class types → `std::shared_ptr<T>`, strings/containers → `const T&`)
- [x] Fix CMake generator argument quoting (`ArgumentList` instead of `Arguments` string)

### Nice to Have
- [x] ~~Indentation preservation in method bodies~~ — emitter concern, not AST; CppEmitter handles this via Indent/Dedent
- [ ] `namespace` support
- [ ] `enum` support
- [ ] Lambda expressions
- [ ] Incremental transpilation (only regenerate changed files)
- [ ] VS Code Problems panel integration for transpiler errors
