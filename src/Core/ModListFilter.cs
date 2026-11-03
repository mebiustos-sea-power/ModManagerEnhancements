using System;

namespace ModManagerEnhancements.Core;

internal sealed class ModListFilter
{
    internal string Query { get; }
    internal ModContent Kind { get; }
    internal bool IsActive => Query.Length != 0 || Kind != ModContent.None;
    private readonly bool wildcard;

    internal ModListFilter(string query, ModContent kind)
    {
        Query = query.Trim(); Kind = kind;
        wildcard = Query.IndexOfAny(new[] { '*', '?' }) >= 0;
    }

    internal bool Matches(string name, string workshopId, ModContent contents) =>
        (Kind == ModContent.None || (contents & Kind) != 0) &&
        (MatchesText(name) || (workshopId.Length != 0 && MatchesText(workshopId)));

    private bool MatchesText(string value)
    {
        if (!wildcard) return value.IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0;
        int item = 0, pattern = 0, star = -1, retry = 0;
        while (item < value.Length)
        {
            if (pattern < Query.Length && (Query[pattern] == '?' ||
                (Query[pattern] != '*' && char.ToUpperInvariant(Query[pattern]) == char.ToUpperInvariant(value[item]))))
            { pattern++; item++; }
            else if (pattern < Query.Length && Query[pattern] == '*')
            { star = pattern++; retry = item; }
            else if (star >= 0) { pattern = star + 1; item = ++retry; }
            else return false;
        }
        while (pattern < Query.Length && Query[pattern] == '*') pattern++;
        return pattern == Query.Length;
    }
}
