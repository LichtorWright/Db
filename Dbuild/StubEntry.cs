using taste;

namespace Dbuild
{
    /// <summary>
    /// The resolved destination-language mapping for a single stub declaration.
    /// Populated by <see cref="StubLoader"/> from <c>[Represents]</c> attributes.
    /// </summary>
    public sealed record StubEntry(
        string       Expression,
        MemberAccess Accessor,
        string?      Include,
        CodePart     Part,
        Language     Language);
}
