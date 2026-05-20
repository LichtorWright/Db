using System;
using System.IO;
using System.Linq;
using System.Text;
using taste;
using taste.Parse.Db;

namespace Dbuild
{
    /// <summary>
    /// Emits a .stub file from a parsed .db source file.
    /// Walks the AST and outputs interface declarations (no method bodies,
    /// no private members, no local variables) — just the surface area
    /// that consumer projects need for type resolution.
    /// 
    /// This is the "self-stubbing" pipeline: .db → .stub
    /// Consumer projects get .stub files for type resolution + .h/.lib for linking.
    /// </summary>
    public class StubEmitter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Parses a .db file and emits the corresponding .stub content.
        /// Returns the stub file content as a string.
        /// </summary>
        public static string EmitStub(string inputPath)
        {
            var parser = new DbParser(inputPath);
            var codeFile = parser.Parse();
            var emitter = new StubEmitter();
            emitter.EmitFile(codeFile);
            return emitter._sb.ToString();
        }

        /// <summary>
        /// Parses a .db file and writes the .stub file next to it (or to a target path).
        /// Returns the output path.
        /// </summary>
        public static string EmitStubToFile(string inputPath, string? outputPath = null)
        {
            string content = EmitStub(inputPath);
            outputPath ??= Path.ChangeExtension(inputPath, ".stub");
            File.WriteAllText(outputPath, content);
            return outputPath;
        }

        // ── Internal ────────────────────────────────────────────────────────

        private void Indent()  { _indent++; }
        private void Dedent()  { _indent--; }
        private void WriteLine(string line = "")
        {
            if (string.IsNullOrEmpty(line))
                _sb.AppendLine();
            else
                _sb.AppendLine(new string(' ', _indent * 4) + line);
        }

        private string Access(AccessModifier access)
            => access == AccessModifier.Public ? "public "
             : access == AccessModifier.Private ? "private "
             : access == AccessModifier.Protected ? "protected "
             : "internal ";

        private string TypeParams(List<string> typeParams)
            => typeParams.Count > 0 ? $"<{string.Join(", ", typeParams)}>" : "";

        private string Params(List<Parameter> parameters)
            => string.Join(", ", parameters.Select(p =>
            {
                string mod = p.Modifier switch
                {
                    "out" => "out ",
                    "ref" => "ref ",
                    "in"  => "in ",
                    "this" => "this ",
                    _ => ""
                };
                string paramStr = $"{mod}{p.Type} {p.Name}";
                if (p.IsParams) paramStr = $"params {paramStr}";
                if (p.Default != null) paramStr += $" = {p.Default}";
                return paramStr;
            }));

        // ── File-level ──────────────────────────────────────────────────────

        private void EmitFile(CodeFile file)
        {
            // using directives
            foreach (var u in file.Usings)
                WriteLine($"using {u.Name};");

            if (file.Usings.Count > 0)
                WriteLine();

            // File-scope directives (includes) — skip these for stubs
            // Stubs don't need C++ includes; those come from [Represents]

            // Top-level classes
            foreach (var cls in file.Classes)
                EmitClass(cls);

            // Top-level enums
            foreach (var e in file.Enums)
                EmitEnum(e);

            // Namespaces
            foreach (var ns in file.Namespaces)
                EmitNamespace(ns);
        }

        // ── Namespace ───────────────────────────────────────────────────────

        private void EmitNamespace(Namespace ns)
        {
            WriteLine($"namespace {ns.Name} {{");
            Indent();

            foreach (var e in ns.Enums) EmitEnum(e);
            foreach (var cls in ns.Classes) EmitClass(cls);
            foreach (var iface in ns.Interfaces) EmitInterface(iface);
            foreach (var st in ns.SumTypes) EmitSumType(st);
            foreach (var mixin in ns.Mixins) EmitMixin(mixin);
            foreach (var ta in ns.TypeAliases) EmitTypeAlias(ta);
            foreach (var del in ns.Delegates) EmitDelegate(del);
            foreach (var nested in ns.NestedNamespaces) EmitNamespace(nested);

            Dedent();
            WriteLine("}");
        }

        // ── Enum ────────────────────────────────────────────────────────────

        private void EmitEnum(EnumDecl e)
        {
            WriteAttributes(e.Attributes);
            WriteLine($"{Access(e.Access)}enum {e.Name}");
            WriteLine("{");
            Indent();
            for (int i = 0; i < e.Members.Count; i++)
            {
                string comma = i < e.Members.Count - 1 ? "," : "";
                WriteLine($"{e.Members[i]}{comma}");
            }
            Dedent();
            WriteLine("}");
        }

        // ── Class ────────────────────────────────────────────────────────────

        private void EmitClass(Class cls)
        {
            WriteAttributes(cls.Attributes);

            string kind = cls.IsStruct ? "struct" : "class";
            string modifiers = "";
            if (cls.IsStatic) modifiers += "static ";
            if (cls.IsSealed) modifiers += "sealed ";
            if (cls.IsAbstract) modifiers += "abstract ";

            string bases = cls.BaseClasses.Count > 0
                ? " : " + string.Join(", ", cls.BaseClasses.Select(b => b.Name))
                : "";

            WriteLine($"{Access(cls.Access)}{modifiers}{kind} {cls.Name}{TypeParams(cls.TypeParams)}{bases} {{");
            Indent();

            // Constants
            foreach (var c in cls.Constants)
                EmitConstant(c);

            // Fields (public only — stubs expose surface area)
            foreach (var f in cls.Fields.Where(f => f.Access == AccessModifier.Public))
                EmitField(f);

            // Properties
            foreach (var p in cls.Properties.Where(p => p.Access == AccessModifier.Public || p.Access == AccessModifier.Protected))
                EmitProperty(p);

            // Methods (public and protected only)
            foreach (var m in cls.Methods.Where(m => m.Access == AccessModifier.Public || m.Access == AccessModifier.Protected))
                EmitMethod(m);

            // Operators (public only)
            foreach (var op in cls.Operators.Where(o => o.Access == AccessModifier.Public))
                EmitOperator(op);

            // Events
            foreach (var ev in cls.Events.Where(ev => ev.Access == AccessModifier.Public || ev.Access == AccessModifier.Protected))
                EmitEvent(ev);

            // Nested enums
            foreach (var ne in cls.NestedEnums)
                EmitEnum(ne);

            // Nested classes
            foreach (var nc in cls.NestedClasses)
                EmitClass(nc);

            // Companion object
            if (cls.Companion != null)
                EmitCompanion(cls.Companion);

            Dedent();
            WriteLine("}");
        }

        // ── Interface ─────────────────────────────────────────────────────────

        private void EmitInterface(InterfaceDeclaration iface)
        {
            WriteAttributes(iface.Attributes);
            string bases = iface.Extends.Count > 0
                ? " : " + string.Join(", ", iface.Extends)
                : "";

            WriteLine($"{Access(iface.Access)}interface {iface.Name}{TypeParams(iface.TypeParams)}{bases} {{");
            Indent();

            foreach (var p in iface.Properties)
                EmitProperty(p);
            foreach (var m in iface.Methods)
                EmitMethod(m);

            Dedent();
            WriteLine("}");
        }

        // ── Sum type ──────────────────────────────────────────────────────────

        private void EmitSumType(SumTypeDeclaration st)
        {
            WriteAttributes(st.Attributes);
            WriteLine($"{Access(st.Access)}sum {st.Name}{TypeParams(st.TypeParams)} {{");
            Indent();
            foreach (var v in st.Variants)
            {
                if (v.Data.Count > 0)
                    WriteLine($"{v.Name}({Params(v.Data)})");
                else
                    WriteLine(v.Name);
            }
            Dedent();
            WriteLine("}");
        }

        // ── Mixin ─────────────────────────────────────────────────────────────

        private void EmitMixin(MixinDeclaration mixin)
        {
            WriteAttributes(mixin.Attributes);
            WriteLine($"mixin {mixin.TargetType} {{");
            Indent();
            foreach (var m in mixin.Methods)
                EmitMethod(m);
            foreach (var p in mixin.Properties)
                EmitProperty(p);
            Dedent();
            WriteLine("}");
        }

        // ── Type alias ────────────────────────────────────────────────────────

        private void EmitTypeAlias(TypeAliasDeclaration ta)
        {
            WriteAttributes(ta.Attributes);
            WriteLine($"{Access(ta.Access)}type {ta.Name} = {ta.TargetType};");
        }

        // ── Delegate ──────────────────────────────────────────────────────────

        private void EmitDelegate(DelegateDecl del)
        {
            WriteAttributes(del.Attributes);
            WriteLine($"{Access(del.Access)}delegate {del.ReturnType} {del.Name}({Params(del.Parameters)});");
        }

        // ── Members ───────────────────────────────────────────────────────────

        private void EmitConstant(Constant c)
        {
            WriteAttributes(c.Attributes);
            WriteLine($"{Access(c.Access)}const {c.Type} {c.Name} = {c.Value};");
        }

        private void EmitField(Field f)
        {
            WriteAttributes(f.Attributes);
            string mods = "";
            if (f.IsStatic) mods += "static ";
            if (f.IsReadonly) mods += "readonly ";
            string init = f.Initializer != null ? $" = {f.Initializer}" : "";
            WriteLine($"{Access(f.Access)}{mods}{f.Type} {f.Name}{init};");
        }

        private void EmitProperty(Property p)
        {
            WriteAttributes(p.Attributes);
            string mods = "";
            if (p.IsStatic) mods += "static ";
            if (p.IsVirtual) mods += "virtual ";
            if (p.IsOverride) mods += "override ";

            string accessors;
            if (p.IsExpressionBodied && p.Initializer != null)
            {
                // Expression-bodied: public int Count => _count;
                WriteLine($"{Access(p.Access)}{mods}{p.Type} {p.Name} => {p.Initializer};");
                return;
            }
            else if (p.HasGetter && p.HasSetter)
                accessors = " { get; set; }";
            else if (p.HasGetter)
                accessors = " { get; }";
            else if (p.HasSetter)
                accessors = " { set; }";
            else
                accessors = "";

            WriteLine($"{Access(p.Access)}{mods}{p.Type} {p.Name}{accessors}");
        }

        private void EmitMethod(Method m)
        {
            WriteAttributes(m.Attributes);

            string mods = "";
            if (m.IsStatic) mods += "static ";
            if (m.IsVirtual) mods += "virtual ";
            if (m.IsAbstract) mods += "abstract ";
            if (m.IsOverride) mods += "override ";
            if (m.IsSealed) mods += "sealed ";
            if (m.IsAsync) mods += "async ";

            string name = m.IsConstructor ? m.Name
                        : m.IsDestructor  ? $"~{m.Name}"
                        : m.Name;

            string typeParams = m.TypeParams.Count > 0 ? $"<{string.Join(", ", m.TypeParams)}>" : "";

            // Stub: no body, just the signature with semicolon
            WriteLine($"{Access(m.Access)}{mods}{m.ReturnType} {name}{typeParams}({Params(m.Parameters)});");
        }

        private void EmitOperator(OperatorOverload op)
        {
            WriteAttributes(op.Attributes);
            WriteLine($"{Access(op.Access)}static {op.ReturnType} operator{op.Operator}({Params(op.Parameters)});");
        }

        private void EmitEvent(Event ev)
        {
            WriteAttributes(ev.Attributes);
            WriteLine($"{Access(ev.Access)}event {ev.DelegateType} {ev.Name};");
        }

        private void EmitCompanion(CompanionObject companion)
        {
            WriteLine($"static class {companion.Name} {{");
            Indent();

            foreach (var c in companion.Constants)
                EmitConstant(c);
            foreach (var f in companion.Fields.Where(f => f.Access == AccessModifier.Public))
                EmitField(f);
            foreach (var p in companion.Properties.Where(p => p.Access == AccessModifier.Public || p.Access == AccessModifier.Protected))
                EmitProperty(p);
            foreach (var m in companion.Methods.Where(m => m.Access == AccessModifier.Public || m.Access == AccessModifier.Protected))
                EmitMethod(m);

            Dedent();
            WriteLine("}");
        }

        // ── Attributes ────────────────────────────────────────────────────────

        private void WriteAttributes(List<SourceAttribute> attributes)
        {
            foreach (var attr in attributes)
            {
                if (attr.Arguments.Count > 0)
                {
                    var args = string.Join(", ", attr.Arguments);
                    WriteLine($"[{attr.Name}({args})]");
                }
                else
                {
                    WriteLine($"[{attr.Name}]");
                }
            }
        }
    }
}