using System;
using taste;
using taste.Emit;

namespace Dbuild
{
    /// <summary>
    /// Maps a Db stub declaration to its equivalent in a destination language.
    /// <para>
    /// On a <b>type</b> (class, struct, enum …):
    /// <code>
    /// [Represents("std::string", CodePart.Class, Language.Cpp, MemberAccess.Dot, "&lt;string&gt;")]
    /// </code>
    /// The fifth argument is the include/import path.  Angle-bracket form
    /// (<c>&lt;header&gt;</c>) becomes a system include; quoted form
    /// (<c>"header.h"</c>) becomes a local include.
    /// </para>
    /// <para>
    /// On a <b>member</b> (method, property, field, constant …):
    /// <code>
    /// [Represents("size()", CodePart.Method, Language.Cpp)]
    /// </code>
    /// </para>
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
        AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property |
        AttributeTargets.Field | AttributeTargets.Constructor,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class RepresentsAttribute : Attribute
    {
        /// <summary>The destination-language expression this declaration maps to.</summary>
        public string Expression { get; }

        /// <summary>The kind of construct in the destination language.</summary>
        public CodePart Part { get; }

        /// <summary>The destination language this stub targets.</summary>
        public Language Language { get; }

        /// <summary>
        /// How members of this type are accessed (type-level only).
        /// Defaults to <see cref="MemberAccess.Dot"/>.
        /// </summary>
        public MemberAccess Accessor { get; }

        /// <summary>
        /// Include / import path required to use this type in the destination language
        /// (type-level only).  Angle-bracket form = system include; quoted form = local.
        /// E.g. <c>"&lt;string&gt;"</c>, <c>"&lt;vector&gt;"</c>, <c>"\"mylib.h\""</c>.
        /// Null when not applicable.
        /// </summary>
        public string? Include { get; }

        /// <summary>
        /// Describes how any code element maps to its equivalent in a destination language.
        /// All parameters apply uniformly regardless of whether this is on a type, method,
        /// property, field, enum value, constant, or any other declaration.
        /// </summary>
        public RepresentsAttribute(
            string       expression,
            CodePart     part,
            Language     language,
            MemberAccess accessor = MemberAccess.Dot,
            string?        include  = null)
        {
            Expression = expression;
            Part       = part;
            Language   = language;
            Accessor   = accessor;
            Include    = include;
        }
    }
}