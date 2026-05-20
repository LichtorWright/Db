using System.Collections.Generic;
using taste;

namespace Dbuild
{
    /// <summary>
    /// Symbol table built from .stub/.db files before user-code parsing begins.
    /// Maps fully-qualified Db names to their destination-language equivalents.
    /// </summary>
    public sealed class StubRegistry
    {
        // "Standard.String" → StubEntry for the type itself
        private readonly Dictionary<string, StubEntry> _types =
            new Dictionary<string, StubEntry>();

        // "Standard.String" → { "Length" → StubEntry, "Slice" → StubEntry, … }
        private readonly Dictionary<string, Dictionary<string, StubEntry>> _members =
            new Dictionary<string, Dictionary<string, StubEntry>>();

        public void RegisterType(string dbFullName, StubEntry entry)
            => _types[dbFullName] = entry;

        public void RegisterMember(string dbTypeName, string memberName, StubEntry entry)
        {
            if (!_members.TryGetValue(dbTypeName, out var map))
                _members[dbTypeName] = map = new Dictionary<string, StubEntry>();
            map[memberName] = entry;
        }

        /// <summary>
        /// Returns the entry for a fully-qualified Db type name, or null.
        /// </summary>
        public bool TryGetType(string dbFullName, out StubEntry entry)
            => _types.TryGetValue(dbFullName, out entry);

        /// <summary>
        /// Returns the entry for a member on a fully-qualified Db type, or null.
        /// </summary>
        public bool TryGetMember(string dbTypeName, string memberName, out StubEntry entry)
        {
            entry = null;
            return _members.TryGetValue(dbTypeName, out var map)
                && map.TryGetValue(memberName, out entry);
        }

        /// <summary>
        /// Resolves a Db type name to its destination-language expression.
        /// Returns the original name if no mapping is found.
        /// Also outputs the required include path (null if none).
        /// </summary>
        public string ResolveType(string dbTypeName, out string include)
        {
            if (_types.TryGetValue(dbTypeName, out var entry))
            {
                include = entry.Include;
                return entry.Expression;
            }
            include = null;
            return dbTypeName;
        }
    }
}
