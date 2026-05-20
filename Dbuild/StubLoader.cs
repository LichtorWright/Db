using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using taste;

namespace Dbuild
{
    /// <summary>
    /// Parses .stub and .db files to build a <see cref="StubRegistry"/>.
    /// Reads <c>[Represents]</c> attributes and registers each type and member mapping.
    /// Must be called before user .db files are parsed.
    /// </summary>
    public sealed class StubLoader
    {
        private readonly Language _targetLanguage;

        public StubLoader(Language targetLanguage = Language.Cpp)
        {
            _targetLanguage = targetLanguage;
        }

        /// <summary>
        /// Loads all .stub and .db files in <paramref name="directory"/> (recursively)
        /// and returns a populated <see cref="StubRegistry"/>.
        /// </summary>
        public StubRegistry LoadDirectory(string directory)
        {
            var registry = new StubRegistry();
            if (!Directory.Exists(directory)) return registry;

            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (string.Equals(ext, ".stub", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".db",   StringComparison.OrdinalIgnoreCase))
                {
                    LoadFile(file, registry);
                }
            }
            return registry;
        }

        // ── per-file parsing ────────────────────────────────────────────────

        public void LoadFile(string path, StubRegistry registry)
        {
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return; }

            int    depth      = 0;   // overall brace depth
            int    classDepth = -1;  // depth at which we are inside the current class body
            string currentClass = null;
            var    nsStack    = new Stack<string>();

            string       pendingExpr     = null;
            MemberAccess pendingAccessor = MemberAccess.Dot;
            string       pendingInclude  = null;
            CodePart     pendingPart     = CodePart.Class;
            Language     pendingLanguage = Language.Cpp;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("///"))
                    continue;

                // ── [Represents(...)] ────────────────────────────────────────
                if (line.StartsWith("[Represents("))
                {
                    TryParseRepresents(line,
                        out pendingExpr, out pendingAccessor,
                        out pendingInclude, out pendingPart, out pendingLanguage);
                    continue;
                }

                // Other attributes: skip but keep any pending Represents
                if (line.StartsWith("[")) continue;

                int net = NetBraces(line);
                depth += net;

                // ── namespace Xxx { ──────────────────────────────────────────
                if (line.StartsWith("namespace ") && net > 0)
                {
                    var m = Regex.Match(line, @"^namespace\s+([\w.]+)");
                    if (m.Success) nsStack.Push(m.Groups[1].Value);
                    pendingExpr = null;
                    continue;
                }

                // ── closing brace ────────────────────────────────────────────
                if (net < 0)
                {
                    if (currentClass != null && depth < classDepth)
                    {
                        currentClass = null;
                        classDepth   = -1;
                    }
                    else if (currentClass == null && nsStack.Count > 0 && depth < nsStack.Count)
                    {
                        nsStack.Pop();
                    }
                    pendingExpr = null;
                    continue;
                }
                // ── class / struct Xxx { ─────────────────────────────────────
                var clsM = Regex.Match(line, @"\b(?:class|struct)\s+(\w+)");
                if (clsM.Success && net > 0)
                {
                    currentClass = clsM.Groups[1].Value;
                    classDepth   = depth;

                    if (pendingExpr != null && pendingLanguage == _targetLanguage)
                    {
                        registry.RegisterType(
                            Fqn(nsStack, currentClass),
                            new StubEntry(pendingExpr, pendingAccessor, pendingInclude, pendingPart, pendingLanguage));
                    }
                    pendingExpr = null;
                    continue;
                }

                // ── enum Xxx ─────────────────────────────────────────────────
                var enumM = Regex.Match(line, @"\benum\s+(\w+)");
                if (enumM.Success && pendingExpr != null && pendingLanguage == _targetLanguage)
                {
                    registry.RegisterType(
                        Fqn(nsStack, enumM.Groups[1].Value),
                        new StubEntry(pendingExpr, pendingAccessor, pendingInclude, pendingPart, pendingLanguage));
                    pendingExpr = null;
                    continue;
                }

                // ── member declaration (net == 0, directly inside class) ─────
                if (currentClass != null && depth == classDepth && pendingExpr != null
                    && pendingLanguage == _targetLanguage)
                {
                    bool   isStatic    = Regex.IsMatch(line, @"\bstatic\b");
                    string memberName  = ExtractMemberName(line);
                    if (memberName != null)
                    {
                        // Default Dot accessor → Colons for statics (emitter output directive)
                        var accessor = pendingAccessor == MemberAccess.Dot && isStatic
                            ? MemberAccess.Colons
                            : pendingAccessor;

                        registry.RegisterMember(
                            Fqn(nsStack, currentClass),
                            memberName,
                            new StubEntry(pendingExpr, accessor, pendingInclude, pendingPart, pendingLanguage));
                    }
                }
                pendingExpr = null;
            }
        }

        // ── [Represents] parsing ─────────────────────────────────────────────

        private static bool TryParseRepresents(
            string       line,
            out string       expression,
            out MemberAccess accessor,
            out string       include,
            out CodePart     part,
            out Language     language)
        {
            expression = null;
            accessor   = MemberAccess.Dot;
            include    = null;
            part       = CodePart.Class;
            language   = Language.Cpp;

            // Extract the argument list between [Represents( … )]
            int open = line.IndexOf("[Represents(", StringComparison.Ordinal);
            if (open < 0) return false;
            int argsStart = open + "[Represents(".Length;

            int depth = 1, i;
            bool inStr = false;
            for (i = argsStart; i < line.Length && depth > 0; i++)
            {
                char c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\')) inStr = !inStr;
                if (!inStr) { if (c == '(') depth++; else if (c == ')') depth--; }
            }
            if (depth != 0) return false;

            string inner = line.Substring(argsStart, i - 1 - argsStart);
            var args = SplitArgs(inner);
            if (args.Count == 0) return false;

            expression = StripQuotes(args[0]);

            if (args.Count == 1)
            {
                // Old single-string format — infer accessor from expression content
                accessor = InferAccessor(expression);
                return true;
            }

            // New 5-arg format: expr, CodePart.Xxx, Language.Xxx, MemberAccess.Xxx, "include"
            if (args.Count >= 2) part     = ParseEnum(args[1], CodePart.Class);
            if (args.Count >= 3) language = ParseEnum(args[2], Language.Cpp);
            if (args.Count >= 4) accessor = ParseEnum(args[3], MemberAccess.Dot);
            if (args.Count >= 5) include  = StripQuotes(args[4]);
            return true;
        }

        /// <summary>
        /// Splits comma-separated attribute arguments, respecting quoted strings and
        /// angle-bracket depth (so "std::map&lt;int,int&gt;" stays as one arg).
        /// </summary>
        private static List<string> SplitArgs(string raw)
        {
            var  args   = new List<string>();
            int  depth  = 0;
            bool inStr  = false;
            int  start  = 0;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '"' && (i == 0 || raw[i - 1] != '\\')) inStr = !inStr;
                if (!inStr)
                {
                    if (c == '<') depth++;
                    else if (c == '>') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        args.Add(raw.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }
            if (start < raw.Length) args.Add(raw.Substring(start).Trim());
            return args;
        }

        private static string StripQuotes(string s) => s.Trim().Trim('"');

        private static T ParseEnum<T>(string s, T fallback) where T : struct, Enum
        {
            int dot = s.LastIndexOf('.');
            string name = dot >= 0 ? s.Substring(dot + 1).Trim() : s.Trim();
            return Enum.TryParse<T>(name, out var result) ? result : fallback;
        }

        private static MemberAccess InferAccessor(string expr)
        {
            if (expr.StartsWith("->*")) return MemberAccess.ArrowAsterisk;
            if (expr.StartsWith("->"))  return MemberAccess.Arrow;
            if (expr.Contains("::"))   return MemberAccess.Colons;
            if (expr.StartsWith("?.")) return MemberAccess.QuestionMarkDot;
            return MemberAccess.Dot;
        }

        // ── member-name extraction ───────────────────────────────────────────

        private static string ExtractMemberName(string line)
        {
            // Indexer: "this["
            if (Regex.IsMatch(line, @"\bthis\s*\[")) return "this[]";

            // Take everything before the first ( or {
            int p = line.IndexOf('(');
            int b = line.IndexOf('{');
            int cut;
            if (p >= 0 && b >= 0) cut = Math.Min(p, b);
            else if (p >= 0) cut = p;
            else if (b >= 0) cut = b;
            else return null;

            string left = line.Substring(0, cut).Trim();
            var words = left.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;

            string name = words[words.Length - 1];
            // Strip generic suffix e.g. Method<T>
            int lt = name.IndexOf('<');
            if (lt > 0) name = name.Substring(0, lt);
            return name;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Counts net brace change on a line (ignoring braces inside string literals).
        /// </summary>
        private static int NetBraces(string line)
        {
            int count = 0;
            bool inStr = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\')) inStr = !inStr;
                if (!inStr)
                {
                    if (c == '{') count++;
                    else if (c == '}') count--;
                }
            }
            return count;
        }

        /// <summary>
        /// Builds a fully-qualified Db name from the namespace stack and a local name.
        /// e.g. nsStack=["Standard"] + "String" → "Standard.String"
        /// </summary>
        private static string Fqn(Stack<string> nsStack, string name)
        {
            if (nsStack.Count == 0) return name;
            var parts = nsStack.ToArray();    // [innermost, …, outermost]
            Array.Reverse(parts);             // [outermost, …, innermost]
            return string.Join(".", parts) + "." + name;
        }
    }
}
