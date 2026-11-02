namespace ModManagerEnhancements.Core;

internal sealed class ModFilter
{
    internal string? Id { get; private set; }
    internal string? Name { get; private set; }

    internal void Toggle(string modId, string modName)
    {
        if (Id != null && ConflictReport.SameId(Id, modId)) Clear();
        else { Id = modId; Name = modName; }
    }

    internal void Clear() { Id = Name = null; }
}
