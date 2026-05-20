using System;
using System.Collections.Generic;
using System.Linq;
using taste;
using taste.Emit;
using taste.Emit.Cpp;
using taste.Parse.Db;

namespace Dbuild
{
    /// <summary>
    /// The Db → C++ transpiler pipeline.
    /// Inherits the universal parse → analyze → emit pattern from <see cref="taste.Transpiler"/>
    /// and wires in the Db-specific parser, semantic analyzer, stub resolution, and C++ emitter.
    /// </summary>
    public class DbTranspiler : taste.Transpiler
    {
        /// <summary>
        /// Project-level memory profile. Defaults to RAII.
        /// Set before calling <see cref="TranspileFile"/> to override.
        /// </summary>
        public MemoryProfile MemoryProfile { get; set; } = MemoryProfile.RAII;

        /// <summary>
        /// Optional pre-loaded stub registry. When set, Db type names that map
        /// to C++ equivalents are substituted before semantic analysis.
        /// </summary>
        public StubRegistry StubRegistry { get; set; }

        protected override CodeFile Parse(string inputPath)
        {
            var parser = new DbParser(inputPath);
            return parser.Parse();
        }

        protected override void Analyze(CodeFile codeFile)
        {
            // Db-specific: resolve stub type mappings before validation
            if (StubRegistry != null)
                ResolveStubs(codeFile, StubRegistry);

            var analyzer = new DbSemanticAnalyzer(codeFile);
            analyzer.Analyze();
            analyzer.ResolveAllocationStrategies(MemoryProfile);

            // Check ownership safety (warnings for raw pointers without cleanup)
            var ownershipWarnings = analyzer.CheckOwnershipSafety();
            foreach (var warning in ownershipWarnings)
                Console.WriteLine($"[WARNING] {warning}");
        }

        protected override string Emit(CodeFile codeFile)
        {
            var emitter = new CppEmitter();
            return emitter.EmitFile(codeFile);
        }

        // ── Db-specific stub resolution ────────────────────────────────────

        /// <summary>
        /// Walks the AST and replaces every Db type name with its C++ equivalent
        /// as recorded in <paramref name="registry"/>. Adds required include
        /// directives to the file.
        /// </summary>
        private void ResolveStubs(CodeFile file, StubRegistry registry)
        {
            var usings = file.Usings.Select(u => u.Name).ToList();
            var pendingIncludes = new HashSet<string>();

            string Resolve(string typeName)
            {
                string resolved = registry.ResolveType(typeName, out string include);
                if (resolved == typeName)
                {
                    foreach (var ns in usings)
                    {
                        string qualified = ns + "." + typeName;
                        resolved = registry.ResolveType(qualified, out include);
                        if (resolved != qualified) break;
                    }
                }
                if (include != null && resolved != typeName)
                    pendingIncludes.Add(include);
                return resolved;
            }

            void ResolveClass(Class cls)
            {
                foreach (var f in cls.Fields)       f.Type = Resolve(f.Type);
                foreach (var p in cls.Properties)   p.Type = Resolve(p.Type);
                foreach (var m in cls.Methods)
                {
                    m.ReturnType = Resolve(m.ReturnType);
                    foreach (var param in m.Parameters) param.Type = Resolve(param.Type);
                    foreach (var v in m.Variables)      v.Type     = Resolve(v.Type);
                }
                foreach (var c in cls.Constants)    c.Type = Resolve(c.Type);
                if (cls.Companion != null)
                {
                    foreach (var f in cls.Companion.Fields)      f.Type = Resolve(f.Type);
                    foreach (var p in cls.Companion.Properties)  p.Type = Resolve(p.Type);
                    foreach (var m in cls.Companion.Methods)
                    {
                        m.ReturnType = Resolve(m.ReturnType);
                        foreach (var param in m.Parameters) param.Type = Resolve(param.Type);
                        foreach (var v in m.Variables)      v.Type     = Resolve(v.Type);
                    }
                    foreach (var c in cls.Companion.Constants) c.Type = Resolve(c.Type);
                }
            }

            foreach (var cls in file.Classes)    ResolveClass(cls);
            foreach (var ns in file.Namespaces)
                foreach (var cls in ns.Classes)  ResolveClass(cls);

            // Emit required include directives
            foreach (var include in pendingIncludes)
            {
                bool   isSystem = include.StartsWith("<") && include.EndsWith(">");
                string target   = isSystem ? include.Substring(1, include.Length - 2) : include;
                bool   already  = file.FileScopeDirectives.Any(d => d.Target == target);
                if (!already)
                    file.FileScopeDirectives.Add(new FileScopeDirective("include", target) { IsSystem = isSystem });
            }
        }
    }

    // ── Backward compatibility ──────────────────────────────────────────────

    /// <summary>
    /// Static facade for the Db → C++ transpiler.
    /// Delegates to <see cref="DbTranspiler"/> for backward compatibility
    /// with existing call sites.
    /// </summary>
    public static class Transpiler
    {
        public static void TranspileFile(string inputPath, string outputPath, StubRegistry registry = null)
        {
            var impl = new DbTranspiler { StubRegistry = registry };
            impl.TranspileFile(inputPath, outputPath);
        }
    }
}