#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using taste;

namespace Dbuild
{
    /// <summary>
    /// Converts C++ header files (.h / .hpp) to Db stub files (.stub).
    /// Handles: namespace, class, struct, union, enum/enum class, typedef/using
    /// aliases, free functions, extern declarations, and #define macros.
    /// Property detection rule: const + non-void return + zero params → { get; }.
    /// Adds [Represents] attributes wherever the Db (PascalCase) name differs from the C++ name.
    /// </summary>
    public class CppHeaderToDbStub : IStubConverter
    {
        // ── IStubConverter ───────────────────────────────────────────────────────

        public string[] WatchExtensions => new[] { "*.h", "*.hpp" };

        void IStubConverter.Convert(string inputPath, string outputPath)
            => Convert(inputPath, outputPath);

        void IStubConverter.ImportTypes(string inputPath, StubRegistry registry)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Header not found: {inputPath}");

            // Parse in-memory, emit stub text, feed directly into registry — no disk I/O
            string raw   = File.ReadAllText(inputPath);
            string clean = StripComments(raw);
            var    file  = new CppParser(clean, Path.GetFileNameWithoutExtension(inputPath)).Parse();
            string stub  = new StubEmitter().Emit(file);

            // Load the emitted stub text into the registry via a temp file
            string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".stub");
            try
            {
                File.WriteAllText(temp, stub);
                new StubLoader().LoadFile(temp, registry);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        // ── Entry point ─────────────────────────────────────────────────────────

        public static void Convert(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Header not found: {inputPath}");

            string raw   = File.ReadAllText(inputPath);
            string clean = StripComments(raw);
            var    file  = new CppParser(clean, Path.GetFileNameWithoutExtension(inputPath)).Parse();
            File.WriteAllText(outputPath, new StubEmitter().Emit(file));
        }

        // ── Comment stripper ────────────────────────────────────────────────────

        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            int i  = 0;
            while (i < src.Length)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    if (i + 1 < src.Length) i += 2;
                    sb.Append(' ');
                    continue;
                }
                if (src[i] == '"')
                {
                    sb.Append(src[i++]);
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) sb.Append(src[i++]);
                        sb.Append(src[i++]);
                    }
                    if (i < src.Length) sb.Append(src[i++]);
                    continue;
                }
                sb.Append(src[i++]);
            }
            return sb.ToString();
        }

        // ── AST types ───────────────────────────────────────────────────────────

        private class CppFile
        {
            public string            SourceName;
            public List<CppDecl>     Decls  = new();
            public List<CppMacro>    Macros = new();
        }

        private class CppMacro  { public string Name; public string Value; }

        private abstract class CppDecl { }

        private class CppNamespace : CppDecl
        {
            public string         Name;
            public List<CppDecl>  Decls  = new();
            public List<CppMacro> Macros = new();
        }

        private class CppType : CppDecl
        {
            public string           Kind;        // "class" | "struct" | "union"
            public string           Name;
            public List<string>     TypeParams = new();
            public List<string>     Bases      = new();
            public List<CppMember>  Members    = new();
        }

        private class CppEnum : CppDecl
        {
            public string       Name;
            public bool         IsClass;
            public string       Underlying;
            public List<string> Values = new();
        }

        private class CppAlias : CppDecl  { public string Alias; public string Target; }

        private class CppFreeFuncs : CppDecl
        {
            public string          OwnerName;
            public List<CppFunc>   Funcs = new();
        }

        private class CppExternVar : CppDecl { public string Type; public string Name; }

        private abstract class CppMember { }

        private class CppField : CppMember
        {
            public string      Type;
            public string      CppName;
            public bool        IsStatic;
            public bool        IsConst;
            public AccessLevel Access;
        }

        private class CppFunc : CppMember
        {
            public string          ReturnType;
            public string          CppName;
            public List<CppParam>  Params     = new();
            public bool            IsConst;
            public bool            IsStatic;
            public bool            IsVirtual;
            public bool            IsPure;
            public bool            IsCtor;
            public bool            IsOperator;
            public AccessLevel     Access;
        }

        private class CppParam { public string Type; public string Name; }

        // Wraps a nested CppDecl (enum, alias, nested class) found inside a class body
        private class CppNestedDecl : CppMember { public CppDecl Decl; }

        private enum AccessLevel { Public, Protected, Private }

        // ── Parser ──────────────────────────────────────────────────────────────

        private class CppParser
        {
            private readonly string _src;
            private readonly string _name;
            private int _pos;

            public CppParser(string src, string name) { _src = src; _name = name; }

            public CppFile Parse()
            {
                var file = new CppFile { SourceName = _name };
                ParseScope(file.Decls, file.Macros, outerType: null, outerName: _name);
                return file;
            }

            // ── Scope parser ─────────────────────────────────────────────────────

            private void ParseScope(List<CppDecl> decls, List<CppMacro> macros,
                                    CppType outerType, string outerName)
            {
                var freeFuncs  = new CppFreeFuncs { OwnerName = outerName };
                var access     = (outerType == null || outerType.Kind == "struct")
                                 ? AccessLevel.Public : AccessLevel.Private;

                while (_pos < _src.Length)
                {
                    SkipWS();
                    if (_pos >= _src.Length || _src[_pos] == '}') break;

                    // Preprocessor
                    if (_src[_pos] == '#')
                    {
                        string dir = ReadLine();
                        TryParseMacro(dir.Trim(), macros);
                        continue;
                    }

                    if (_src[_pos] == ';') { _pos++; continue; }

                    // Optional template<...> prefix
                    List<string> typeParams = null;
                    int savedPos = _pos;
                    if (PeekWord() == "template")
                    {
                        ReadWord();
                        typeParams = ReadTemplateParams();
                        SkipWS();
                    }
                    else _pos = savedPos;

                    string kw = PeekWord();

                    // Access specifier (inside a class body)
                    if (outerType != null &&
                        (kw == "public" || kw == "private" || kw == "protected"))
                    {
                        ReadWord(); SkipWS();
                        if (_pos < _src.Length && _src[_pos] == ':') _pos++;
                        access = kw == "public"    ? AccessLevel.Public
                               : kw == "protected" ? AccessLevel.Protected
                                                   : AccessLevel.Private;
                        continue;
                    }

                    // namespace
                    if (kw == "namespace")
                    {
                        ReadWord(); SkipWS();
                        string nsName = ReadWord(); SkipWS();
                        var ns = new CppNamespace { Name = nsName };
                        if (_pos < _src.Length && _src[_pos] == '{')
                        {
                            _pos++;
                            ParseScope(ns.Decls, ns.Macros, outerType: null, outerName: nsName);
                            if (_pos < _src.Length && _src[_pos] == '}') _pos++;
                        }
                        AddDecl(decls, outerType, ns);
                        continue;
                    }

                    // class / struct / union
                    if (kw == "class" || kw == "struct" || kw == "union")
                    {
                        ReadWord(); SkipWS();
                        while (_pos < _src.Length && _src[_pos] == '[') SkipAttribute();
                        SkipWS();
                        string typeName = ReadWord();
                        if (string.IsNullOrEmpty(typeName)) { SkipToSemiOrBrace(); continue; }
                        SkipWS();
                        // Forward declaration
                        if (_pos < _src.Length && _src[_pos] == ';') { _pos++; continue; }

                        var type = new CppType { Kind = kw, Name = typeName };
                        if (typeParams != null) type.TypeParams.AddRange(typeParams);

                        if (_pos < _src.Length && _src[_pos] == ':')
                        {
                            _pos++;
                            type.Bases.AddRange(ParseBases());
                        }
                        SkipWS();
                        if (_pos < _src.Length && _src[_pos] == '{')
                        {
                            _pos++;
                            ParseScope(new List<CppDecl>(), null, type, typeName);
                            SkipWS();
                            if (_pos < _src.Length && _src[_pos] == '}') _pos++;
                        }
                        SkipToSemi();
                        AddDecl(decls, outerType, type);
                        continue;
                    }

                    // enum / enum class / enum struct
                    if (kw == "enum")
                    {
                        ReadWord(); SkipWS();
                        bool isClass = PeekWord() == "class" || PeekWord() == "struct";
                        if (isClass) ReadWord();
                        SkipWS();
                        string enumName = ReadWord(); SkipWS();
                        string underlying = null;
                        if (_pos < _src.Length && _src[_pos] == ':')
                        {
                            _pos++; SkipWS();
                            underlying = ReadTypeToken(); SkipWS();
                        }
                        var en = new CppEnum { Name = enumName, IsClass = isClass, Underlying = underlying };
                        if (_pos < _src.Length && _src[_pos] == '{')
                        {
                            _pos++;
                            en.Values.AddRange(ParseEnumValues());
                            if (_pos < _src.Length && _src[_pos] == '}') _pos++;
                        }
                        SkipToSemi();
                        AddDecl(decls, outerType, en);
                        continue;
                    }

                    // typedef
                    if (kw == "typedef")
                    {
                        ReadWord(); SkipWS();
                        string rest = ReadToSemi().Trim();
                        var parts = rest.Split(new[] { ' ', '\t', '\n', '\r' },
                                               StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var alias = new CppAlias
                            {
                                Alias  = parts[^1],
                                Target = string.Join(" ", parts[..^1])
                            };
                            AddDecl(decls, outerType, alias);
                        }
                        continue;
                    }

                    // using Name = Type;
                    if (kw == "using")
                    {
                        ReadWord(); SkipWS();
                        string aliasName = ReadWord(); SkipWS();
                        if (_pos < _src.Length && _src[_pos] == '=')
                        {
                            _pos++;
                            string target = ReadToSemi().Trim();
                            AddDecl(decls, outerType, new CppAlias { Alias = aliasName, Target = target });
                        }
                        else ReadToSemi(); // using declaration (not alias)
                        continue;
                    }

                    // extern
                    if (kw == "extern")
                    {
                        ReadWord(); SkipWS();
                        if (_pos < _src.Length && _src[_pos] == '"') { ReadToSemi(); continue; }
                        string rest = ReadToSemi().Trim();
                        var m = Regex.Match(rest, @"^(.+)\s+(\w+)\s*$");
                        if (m.Success)
                            AddDecl(decls, outerType,
                                new CppExternVar { Type = m.Groups[1].Value.Trim(), Name = m.Groups[2].Value });
                        continue;
                    }

                    // friend — skip
                    if (kw == "friend") { ReadToSemiOrBrace(out _); continue; }

                    // Function or variable member
                    _pos = savedPos;
                    string stmt = ReadToSemiOrBrace(out bool hadBrace);
                    if (hadBrace) SkipBody();

                    var func = TryParseFunc(stmt, access, outerType?.Name);
                    if (func != null)
                    {
                        if (outerType != null) outerType.Members.Add(func);
                        else freeFuncs.Funcs.Add(func);
                        continue;
                    }

                    var field = TryParseField(stmt, access);
                    if (field != null && outerType != null)
                        outerType.Members.Add(field);
                }

                if (freeFuncs.Funcs.Count > 0) decls.Add(freeFuncs);
            }

            // Route a decl to the right container depending on whether we're inside a class
            private static void AddDecl(List<CppDecl> decls, CppType outerType, CppDecl decl)
            {
                if (outerType != null) outerType.Members.Add(new CppNestedDecl { Decl = decl });
                else                   decls.Add(decl);
            }

            // ── Sub-parsers ──────────────────────────────────────────────────────

            private List<string> ReadTemplateParams()
            {
                var result = new List<string>();
                if (_pos >= _src.Length || _src[_pos] != '<') return result;
                _pos++;
                int depth = 1;
                var cur   = new StringBuilder();
                while (_pos < _src.Length && depth > 0)
                {
                    char c = _src[_pos++];
                    if      (c == '<') { depth++; cur.Append(c); }
                    else if (c == '>')
                    {
                        if (--depth > 0) cur.Append(c);
                    }
                    else if (c == ',' && depth == 1)
                    {
                        AddTemplateParam(cur.ToString(), result);
                        cur.Clear();
                    }
                    else cur.Append(c);
                }
                AddTemplateParam(cur.ToString(), result);
                return result;
            }

            private static void AddTemplateParam(string raw, List<string> result)
            {
                var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string name = parts.Length > 1 ? parts[^1] : raw.Trim();
                if (!string.IsNullOrEmpty(name)) result.Add(name.TrimStart('.'));
            }

            private List<string> ParseBases()
            {
                var result = new List<string>();
                while (_pos < _src.Length && _src[_pos] != '{' && _src[_pos] != ';')
                {
                    SkipWS();
                    string w = PeekWord();
                    if (w == "public" || w == "private" || w == "protected" || w == "virtual")
                    { ReadWord(); SkipWS(); continue; }
                    if (_pos < _src.Length && _src[_pos] == ',') { _pos++; continue; }
                    string baseName = ReadTypeToken();
                    if (!string.IsNullOrEmpty(baseName)) result.Add(baseName);
                }
                return result;
            }

            private List<string> ParseEnumValues()
            {
                var result = new List<string>();
                while (_pos < _src.Length && _src[_pos] != '}')
                {
                    SkipWS();
                    if (_pos >= _src.Length || _src[_pos] == '}') break;
                    if (_src[_pos] == '#') { ReadLine(); continue; }
                    string name = ReadWord();
                    if (!string.IsNullOrEmpty(name)) result.Add(name);
                    SkipWS();
                    if (_pos < _src.Length && _src[_pos] == '=')
                    {
                        _pos++;
                        while (_pos < _src.Length && _src[_pos] != ',' && _src[_pos] != '}') _pos++;
                    }
                    SkipWS();
                    if (_pos < _src.Length && _src[_pos] == ',') _pos++;
                }
                return result;
            }

            private CppFunc TryParseFunc(string stmt, AccessLevel access, string className)
            {
                stmt = stmt.Trim().TrimEnd(';').Trim();
                if (string.IsNullOrWhiteSpace(stmt)) return null;

                stmt = StripModifiers(stmt, out bool isStatic, out bool isVirtual);

                bool isPure = false;
                if (stmt.EndsWith("= 0"))          { isPure = true; stmt = stmt[..^3].Trim(); }
                else if (stmt.EndsWith("= delete") || stmt.EndsWith("= default")) return null;

                int parenOpen = FindFirstParenOutsideAngles(stmt);
                if (parenOpen < 0) return null;

                string before     = stmt[..parenOpen].Trim();
                int    parenClose = FindMatchingParen(stmt, parenOpen);
                if (parenClose < 0) return null;

                string paramStr = stmt[(parenOpen + 1)..parenClose];
                string after    = stmt[(parenClose + 1)..].Trim()
                                     .Replace("noexcept", "").Replace("override", "")
                                     .Replace("final", "").Trim();
                bool isConst = Regex.IsMatch(after, @"\bconst\b");

                // Destructor — skip
                if (Regex.IsMatch(before.TrimStart(), @"^~")) return null;

                // Operator overload
                bool isOperator = false;
                string retType, funcName;
                var opM = Regex.Match(before, @"\boperator\s*(.+)$");
                if (opM.Success)
                {
                    isOperator = true;
                    retType    = before[..opM.Index].Trim();
                    funcName   = "operator" + opM.Groups[1].Value.Trim();
                }
                else
                {
                    var nameM = Regex.Match(before, @"^(.*?)\s*(\w+)\s*$");
                    if (!nameM.Success) return null;
                    retType  = nameM.Groups[1].Value.Trim();
                    funcName = nameM.Groups[2].Value;
                }

                bool isCtor = string.IsNullOrEmpty(retType)
                           || (className != null && funcName == className);
                if (isCtor) retType = "";

                var func = new CppFunc
                {
                    CppName    = funcName,
                    ReturnType = retType,
                    IsConst    = isConst,
                    IsStatic   = isStatic,
                    IsVirtual  = isVirtual,
                    IsPure     = isPure,
                    IsCtor     = isCtor,
                    IsOperator = isOperator,
                    Access     = access,
                };
                func.Params.AddRange(ParseParams(paramStr));
                return func;
            }

            private CppField TryParseField(string stmt, AccessLevel access)
            {
                stmt = stmt.Trim().TrimEnd(';').Trim();
                if (string.IsNullOrWhiteSpace(stmt) || stmt.Contains('(')) return null;

                bool isConst  = Regex.IsMatch(stmt, @"\bconst\b") || Regex.IsMatch(stmt, @"\bconstexpr\b");
                stmt = Regex.Replace(stmt, @"\b(const|constexpr|static|volatile|mutable|inline)\b", "").Trim();
                bool isStatic = Regex.IsMatch(stmt, @"\bstatic\b");

                stmt = Regex.Replace(stmt, @"\[.*?\]", "").Trim(); // strip array dims

                var m = Regex.Match(stmt.Trim(), @"^(.+?)\s+(\w+)\s*$");
                if (!m.Success) return null;
                return new CppField
                {
                    Type     = m.Groups[1].Value.Trim(),
                    CppName  = m.Groups[2].Value,
                    IsStatic = isStatic,
                    IsConst  = isConst,
                    Access   = access,
                };
            }

            private List<CppParam> ParseParams(string paramStr)
            {
                var result = new List<CppParam>();
                if (string.IsNullOrWhiteSpace(paramStr) || paramStr.Trim() == "void") return result;

                foreach (string part in SplitByComma(paramStr))
                {
                    string p = part.Trim();
                    if (string.IsNullOrEmpty(p)) continue;

                    int eq = FindDefaultSeparator(p);
                    if (eq >= 0) p = p[..eq].Trim();

                    p = Regex.Replace(p, @"\b(const|volatile)\b", "").Trim();

                    var nm = Regex.Match(p, @"^(.+?)\s*[&*]?\s*(\w+)\s*$");
                    if (nm.Success && nm.Groups[1].Value.Trim().Length > 0)
                        result.Add(new CppParam
                        {
                            Type = nm.Groups[1].Value.TrimEnd('&', '*', ' '),
                            Name = nm.Groups[2].Value,
                        });
                    else
                        result.Add(new CppParam { Type = p.TrimEnd('&', '*', ' '), Name = "_" });
                }
                return result;
            }

            private static void TryParseMacro(string directive, List<CppMacro> macros)
            {
                if (macros == null) return;
                var m = Regex.Match(directive, @"^#\s*define\s+(\w+)\s+(.+)$");
                if (m.Success)
                    macros.Add(new CppMacro { Name = m.Groups[1].Value, Value = m.Groups[2].Value.Trim() });
            }

            // ── Tokenizer helpers ────────────────────────────────────────────────

            private string PeekWord()  { int s = _pos; string w = ReadWord(); _pos = s; return w; }

            private string ReadWord()
            {
                SkipWS();
                var sb = new StringBuilder();
                while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
                    sb.Append(_src[_pos++]);
                return sb.ToString();
            }

            private string ReadTypeToken()
            {
                SkipWS();
                var sb = new StringBuilder();
                while (_pos < _src.Length)
                {
                    char c = _src[_pos];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == ':') sb.Append(_src[_pos++]);
                    else if (c == '<') sb.Append(ReadBalancedAngle());
                    else break;
                }
                return sb.ToString();
            }

            private string ReadBalancedAngle()
            {
                if (_pos >= _src.Length || _src[_pos] != '<') return "";
                var sb = new StringBuilder();
                sb.Append(_src[_pos++]);
                int depth = 1;
                while (_pos < _src.Length && depth > 0)
                {
                    char c = _src[_pos++];
                    if      (c == '<') depth++;
                    else if (c == '>') depth--;
                    sb.Append(c);
                }
                return sb.ToString();
            }

            private string ReadToSemi()
            {
                var sb = new StringBuilder();
                while (_pos < _src.Length && _src[_pos] != ';') sb.Append(_src[_pos++]);
                if (_pos < _src.Length) _pos++;
                return sb.ToString();
            }

            private string ReadToSemiOrBrace(out bool hadBrace)
            {
                var sb    = new StringBuilder();
                int depth = 0;
                hadBrace  = false;
                while (_pos < _src.Length)
                {
                    char c = _src[_pos];
                    if (c == ';' && depth == 0) { _pos++; break; }
                    if (c == '{')               { hadBrace = true; _pos++; break; }
                    if (c == '(' || c == '<') depth++;
                    if (c == ')' || c == '>') depth--;
                    sb.Append(_src[_pos++]);
                }
                return sb.ToString();
            }

            private void SkipBody()
            {
                int depth = 1;
                while (_pos < _src.Length && depth > 0)
                {
                    char c = _src[_pos++];
                    if      (c == '{') depth++;
                    else if (c == '}') depth--;
                }
                SkipWS();
                if (_pos < _src.Length && _src[_pos] == ';') _pos++;
            }

            private void SkipToSemi()
            {
                while (_pos < _src.Length && _src[_pos] != ';') _pos++;
                if (_pos < _src.Length) _pos++;
            }

            private void SkipToSemiOrBrace()
            {
                while (_pos < _src.Length && _src[_pos] != ';' && _src[_pos] != '{') _pos++;
                if (_pos < _src.Length) _pos++;
            }

            private void SkipAttribute()
            {
                int depth = 0;
                while (_pos < _src.Length)
                {
                    if      (_src[_pos] == '[') { depth++; _pos++; }
                    else if (_src[_pos] == ']') { _pos++; if (--depth == 0) break; }
                    else    _pos++;
                }
            }

            private string ReadLine()
            {
                var sb = new StringBuilder();
                while (_pos < _src.Length && _src[_pos] != '\n')
                {
                    if (_src[_pos] == '\\' && _pos + 1 < _src.Length && _src[_pos + 1] == '\n')
                    { _pos += 2; continue; }
                    sb.Append(_src[_pos++]);
                }
                if (_pos < _src.Length) _pos++;
                return sb.ToString();
            }

            private void SkipWS()
            {
                while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos])) _pos++;
            }

            // ── Utility ──────────────────────────────────────────────────────────

            private static readonly string[] _leadingMods =
            {
                "inline", "constexpr", "consteval", "constinit", "explicit",
                "virtual", "static", "mutable", "volatile", "[[nodiscard]]",
                "[[maybe_unused]]", "[[deprecated]]",
            };
            private static readonly string[] _trailingMods =
                { "override", "final", "noexcept" };

            private static string StripModifiers(string stmt, out bool isStatic, out bool isVirtual)
            {
                isStatic  = false;
                isVirtual = false;
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (string mod in _leadingMods)
                    {
                        if (!StartsWithMod(stmt, mod)) continue;
                        if (mod == "static")  isStatic  = true;
                        if (mod == "virtual") isVirtual = true;
                        stmt    = stmt[mod.Length..].TrimStart();
                        changed = true;
                    }
                    foreach (string mod in _trailingMods)
                    {
                        if (!stmt.EndsWith(mod)) continue;
                        stmt    = stmt[..^mod.Length].TrimEnd();
                        changed = true;
                    }
                }
                return stmt;
            }

            private static bool StartsWithMod(string s, string mod) =>
                s.StartsWith(mod) && s.Length > mod.Length &&
                !char.IsLetterOrDigit(s[mod.Length]) && s[mod.Length] != '_';

            private static int FindFirstParenOutsideAngles(string s)
            {
                int depth = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if      (s[i] == '<') depth++;
                    else if (s[i] == '>') depth--;
                    else if (s[i] == '(' && depth == 0) return i;
                }
                return -1;
            }

            private static int FindMatchingParen(string s, int open)
            {
                int depth = 1;
                for (int i = open + 1; i < s.Length; i++)
                {
                    if      (s[i] == '(') depth++;
                    else if (s[i] == ')') { if (--depth == 0) return i; }
                }
                return -1;
            }

            private static int FindDefaultSeparator(string s)
            {
                int depth = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '<' || s[i] == '(' || s[i] == '[') depth++;
                    else if (s[i] == '>' || s[i] == ')' || s[i] == ']') depth--;
                    else if (s[i] == '=' && depth == 0) return i;
                }
                return -1;
            }

            private static List<string> SplitByComma(string s)
            {
                var result = new List<string>();
                int depth  = 0;
                var cur    = new StringBuilder();
                foreach (char c in s)
                {
                    if      (c == '<' || c == '(' || c == '[') depth++;
                    else if (c == '>' || c == ')' || c == ']') depth--;
                    else if (c == ',' && depth == 0) { result.Add(cur.ToString()); cur.Clear(); continue; }
                    cur.Append(c);
                }
                if (cur.Length > 0) result.Add(cur.ToString());
                return result;
            }
        }

        // ── Emitter ─────────────────────────────────────────────────────────────

        private class StubEmitter
        {
            private readonly StringBuilder _sb = new();
            private int    _indent;
            private string _include; // e.g. "SDL.h" (local) or "<string>" (system)

            public string Emit(CppFile file)
            {
                _include = $"{file.SourceName}.h";
                _sb.AppendLine($"// Auto-generated stub from {file.SourceName}.h — do not edit manually");
                _sb.AppendLine();
                foreach (var m in file.Macros) EmitMacro(m);
                if (file.Macros.Count > 0) _sb.AppendLine();
                foreach (var d in file.Decls) EmitDecl(d);
                return _sb.ToString();
            }

            private void EmitDecl(CppDecl d)
            {
                switch (d)
                {
                    case CppNamespace ns:   EmitNamespace(ns);  break;
                    case CppType type:      EmitType(type);     break;
                    case CppEnum en:        EmitEnum(en);       break;
                    case CppAlias alias:    EmitAlias(alias);   break;
                    case CppFreeFuncs ff:   EmitFreeFuncs(ff);  break;
                    case CppExternVar ext:  EmitExtern(ext);    break;
                }
            }

            private void EmitNamespace(CppNamespace ns)
            {
                Line($"namespace {ns.Name}");
                Line("{");
                _indent++;
                foreach (var m in ns.Macros) EmitMacro(m);
                foreach (var d in ns.Decls)  EmitDecl(d);
                _indent--;
                Line("}");
                _sb.AppendLine();
            }

            private void EmitType(CppType type)
            {
                string dbKind    = type.Kind == "union" ? "struct" : type.Kind;
                string codePart  = type.Kind switch { "struct" => "CodePart.Struct", "union" => "CodePart.Union", _ => "CodePart.Class" };
                string tParams   = type.TypeParams.Count > 0
                                   ? $"<{string.Join(", ", type.TypeParams)}>" : "";
                string bases     = type.Bases.Count > 0
                                   ? " : " + string.Join(", ", type.Bases.Select(MapType)) : "";
                Line($"[Represents(\"{type.Name}\", {codePart}, Language.Cpp, MemberAccess.Dot, \"{_include}\")]");
                Line($"public {dbKind} {type.Name}{tParams}{bases}");
                Line("{");
                _indent++;
                foreach (var member in type.Members)
                    EmitMember(member, type.Name);
                _indent--;
                Line("}");
                _sb.AppendLine();
            }

            private void EmitMember(CppMember member, string className)
            {
                switch (member)
                {
                    case CppFunc  f when f.Access  == AccessLevel.Public: EmitFunc(f, className);  break;
                    case CppField fd when fd.Access == AccessLevel.Public: EmitField(fd);           break;
                    case CppNestedDecl nd: EmitDecl(nd.Decl);                                       break;
                }
            }

            private void EmitFunc(CppFunc f, string className)
            {
                if (f.IsCtor)
                {
                    string ps = ParamStr(f.Params);
                    Line($"[Represents(\"{className}\", CodePart.Constructor, Language.Cpp, MemberAccess.None)]");
                    Line($"/// <summary>Constructs a new {className}.</summary>");
                    Line($"public {className}({ps});");
                    return;
                }
                if (f.IsOperator) return; // skip operator overloads for now

                string dbName   = ToPascalCase(f.CppName);
                string retType  = MapType(f.ReturnType);
                bool   isProp   = f.IsConst && !string.IsNullOrEmpty(f.ReturnType)
                                  && f.ReturnType != "void" && f.Params.Count == 0;
                string staticK  = f.IsStatic ? "static " : "";
                string accessor = f.IsStatic ? "MemberAccess.Colons" : "MemberAccess.Dot";

                if (isProp)
                {
                    Line($"[Represents(\"{f.CppName}()\", CodePart.Property, Language.Cpp, {accessor})]");
                    Line($"/// <summary>Gets {dbName.ToLower()}.</summary>");
                    Line($"public {staticK}{retType} {dbName} {{ get; }}");
                }
                else
                {
                    string ps = ParamStr(f.Params);
                    Line($"[Represents(\"{f.CppName}\", CodePart.Method, Language.Cpp, {accessor})]");
                    Line($"/// <summary>{dbName}.</summary>");
                    Line($"public {staticK}{retType} {dbName}({ps});");
                }
            }

            private void EmitField(CppField fd)
            {
                string dbName   = ToPascalCase(fd.CppName);
                string type     = MapType(fd.Type);
                string setter   = fd.IsConst ? "" : " set;";
                string staticK  = fd.IsStatic ? "static " : "";
                string accessor = fd.IsStatic ? "MemberAccess.Colons" : "MemberAccess.Dot";
                Line($"[Represents(\"{fd.CppName}\", CodePart.Field, Language.Cpp, {accessor})]");
                Line($"/// <summary>{dbName}.</summary>");
                Line($"public {staticK}{type} {dbName} {{ get;{setter} }}");
            }

            private void EmitEnum(CppEnum en)
            {
                string codePart      = en.IsClass ? "CodePart.EnumClass" : "CodePart.Enum";
                string valueAccessor = en.IsClass ? "MemberAccess.Colons" : "MemberAccess.None";
                string underlying    = en.Underlying != null ? $" : {MapType(en.Underlying)}" : "";
                Line($"[Represents(\"{en.Name}\", {codePart}, Language.Cpp, MemberAccess.Colons, \"{_include}\")]");
                Line($"public enum {en.Name}{underlying}");
                Line("{");
                _indent++;
                foreach (string v in en.Values)
                {
                    Line($"[Represents(\"{v}\", CodePart.EnumMember, Language.Cpp, {valueAccessor})]");
                    Line($"{v},");
                }
                _indent--;
                Line("}");
                _sb.AppendLine();
            }

            private void EmitAlias(CppAlias alias)
            {
                Line($"[Represents(\"{alias.Target}\", CodePart.TypeAlias, Language.Cpp, MemberAccess.None)]");
                Line($"public type {alias.Alias} = {MapType(alias.Target)};");
            }

            private void EmitFreeFuncs(CppFreeFuncs ff)
            {
                if (ff.Funcs.Count == 0) return;
                string className = ToPascalCase(ff.OwnerName) + "Functions";
                Line($"[Represents(\"\", CodePart.FreeFunction, Language.Cpp, MemberAccess.None, \"{_include}\")]");
                Line($"public static class {className}");
                Line("{");
                _indent++;
                foreach (var f in ff.Funcs) EmitFunc(f, null);
                _indent--;
                Line("}");
                _sb.AppendLine();
            }

            private void EmitExtern(CppExternVar ext)
            {
                string dbName = ToPascalCase(ext.Name);
                string type   = MapType(ext.Type);
                Line($"[Represents(\"{ext.Name}\", CodePart.ExternVariable, Language.Cpp, MemberAccess.None)]");
                Line($"public static {type} {dbName} {{ get; }}");
            }

            private void EmitMacro(CppMacro macro)
            {
                string val  = macro.Value;
                string type = Regex.IsMatch(val, @"^-?\d+[uUlL]*$")           ? "int"
                            : Regex.IsMatch(val, @"^-?\d+\.\d+[fFdD]?$")      ? "double"
                            : (val == "true" || val == "false")                ? "bool"
                            :                                                    "auto";
                Line($"[Represents(\"{macro.Name}\", CodePart.Constant, Language.Cpp, MemberAccess.None)]");
                Line($"public const {type} {macro.Name} = {val};");
            }

            private string ParamStr(List<CppParam> parms) =>
                string.Join(", ", parms.Select(p => $"{MapType(p.Type)} {ToPascalCase(p.Name)}"));

            private void Line(string text)
            {
                _sb.Append(new string(' ', _indent * 4));
                _sb.AppendLine(text);
            }
        }

        // ── Type mapping ────────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> _typeMap = new(StringComparer.Ordinal)
        {
            { "void",           "void"    },
            { "bool",           "Boolean" },
            { "char",           "Char"    },
            { "int",            "Int32"   },
            { "long",           "Int64"   },
            { "short",          "Int16"   },
            { "float",          "Single"  },
            { "double",         "Double"  },
            { "unsigned int",   "UInt32"  },
            { "unsigned long",  "UInt64"  },
            { "unsigned short", "UInt16"  },
            { "unsigned char",  "Byte"    },
            { "signed char",    "SByte"   },
            { "int8_t",         "SByte"   },
            { "int16_t",        "Int16"   },
            { "int32_t",        "Int32"   },
            { "int64_t",        "Int64"   },
            { "uint8_t",        "Byte"    },
            { "uint16_t",       "UInt16"  },
            { "uint32_t",       "UInt32"  },
            { "uint64_t",       "UInt64"  },
            { "size_t",         "UInt64"  },
            { "ptrdiff_t",      "Int64"   },
            { "std::string",      "String"     },
            { "std::string_view", "StringView" },
            { "std::wstring",     "String"     },
        };

        private static readonly Dictionary<string, string> _templateMap = new(StringComparer.Ordinal)
        {
            { "std::vector",          "Vector"        },
            { "std::list",            "List"          },
            { "std::deque",           "Deque"         },
            { "std::forward_list",    "ForwardList"   },
            { "std::map",             "Map"           },
            { "std::multimap",        "MultiMap"      },
            { "std::unordered_map",   "UnorderedMap"  },
            { "std::set",             "Set"           },
            { "std::multiset",        "Multiset"      },
            { "std::unordered_set",   "UnorderedSet"  },
            { "std::stack",           "Stack"         },
            { "std::queue",           "Queue"         },
            { "std::priority_queue",  "PriorityQueue" },
            { "std::pair",            "Pair"          },
            { "std::tuple",           "Tuple"         },
            { "std::optional",        "Optional"      },
            { "std::shared_ptr",      "SharedPtr"     },
            { "std::unique_ptr",      "UniquePtr"     },
            { "std::weak_ptr",        "WeakPtr"       },
            { "std::array",           "Array"         },
            { "std::bitset",          "Bitset"        },
            { "std::function",        "Function"      },
        };

        private static string MapType(string cppType)
        {
            if (string.IsNullOrWhiteSpace(cppType)) return "void";

            // Strip outer const / volatile / refs / pointers
            string t = cppType
                .Replace("const ",    "").Replace(" const",    "")
                .Replace("volatile ", "").Replace(" volatile", "")
                .Trim().TrimEnd('&', '*', ' ').Trim();

            if (_typeMap.TryGetValue(t, out string mapped)) return mapped;

            // Template: "std::vector<int>" → "Vector<Int32>"
            int angle = t.IndexOf('<');
            if (angle > 0)
            {
                string outer = t[..angle].Trim();
                string inner = t[(angle + 1)..t.LastIndexOf('>')].Trim();
                if (_templateMap.TryGetValue(outer, out string dbOuter))
                {
                    string args = string.Join(", ", SplitTypeArgs(inner).Select(MapType));
                    return $"{dbOuter}<{args}>";
                }
            }

            // Strip std:: prefix for anything not in the map
            if (t.StartsWith("std::")) return t[5..];
            return t;
        }

        private static List<string> SplitTypeArgs(string s)
        {
            var result = new List<string>();
            int depth  = 0;
            var cur    = new StringBuilder();
            foreach (char c in s)
            {
                if      (c == '<') depth++;
                else if (c == '>') depth--;
                else if (c == ',' && depth == 0) { result.Add(cur.ToString().Trim()); cur.Clear(); continue; }
                cur.Append(c);
            }
            if (cur.Length > 0) result.Add(cur.ToString().Trim());
            return result;
        }

        // ── Name mapping ────────────────────────────────────────────────────────

        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            // Already PascalCase (starts uppercase, no underscores) → leave alone
            if (char.IsUpper(name[0]) && !name.Contains('_')) return name;
            // snake_case → PascalCase
            return string.Concat(
                name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Length == 0 ? "" : char.ToUpper(p[0]) + p[1..]));
        }
    }
}