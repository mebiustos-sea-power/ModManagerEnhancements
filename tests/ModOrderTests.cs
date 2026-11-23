using System;
using System.Linq;
using ModManagerEnhancements.Core;

internal static class ModOrderTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(Action<string, Action> test)
    {
        test("enabled grouping preserves both relative orders and original objects for every selection pattern", () =>
        {
            for (int count = 0; count <= 7; count++)
            for (int pattern = 0; pattern < (1 << count); pattern++)
            {
                var mods = Enumerable.Range(0, count).Select(i => new Entry((pattern & (1 << i)) != 0)).ToArray();
                var before = mods.ToArray(); var flags = mods.Select(item => item.Enabled).ToArray();
                var ordered = ModOrder.EnabledFirst(mods, item => item.Enabled, item => item.BaseGame);
                Require(ordered.Length == count && ordered.TakeWhile(item => item.Enabled).Count() == flags.Count(flag => flag),
                    "Enabled and disabled mods were not separated without losing entries.");
                foreach (bool enabled in new[] { true, false })
                    Require(ordered.Where(item => item.Enabled == enabled).SequenceEqual(before.Where(item => item.Enabled == enabled)),
                        "Grouping changed a relative order or replaced a mod object.");
                Require(mods.SequenceEqual(before) && mods.Select(item => item.Enabled).SequenceEqual(flags),
                    "Planning the order changed the input or enabled states.");
            }
        });
        test("enabled grouping uses the latest manual order and checkbox states on each request", () =>
        {
            var first = new Entry(true); var second = new Entry(false); var third = new Entry(true);
            var initial = ModOrder.EnabledFirst(new[] { first, second, third }, item => item.Enabled, item => item.BaseGame);
            Require(initial.SequenceEqual(new[] { first, third, second }), "The initial order is incorrect.");
            first.Enabled = false; second.Enabled = true;
            var ordered = ModOrder.EnabledFirst(new[] { second, third, first }, item => item.Enabled, item => item.BaseGame);
            Require(ordered.SequenceEqual(new[] { second, third, first }), "Grouping reused an earlier order or checkbox snapshot.");
        });
        test("base game stays last for every initial position selection state and normal-mod grouping", () =>
        {
            for (int count = 0; count <= 5; count++)
            for (int pattern = 0; pattern < (1 << count); pattern++)
            for (int position = 0; position <= count; position++)
            foreach (bool enabled in new[] { true, false })
            {
                var mods = Enumerable.Range(0, count).Select(i => new Entry((pattern & (1 << i)) != 0)).ToList();
                var baseGame = new Entry(enabled, true); mods.Insert(position, baseGame);
                var flags = mods.Select(item => item.Enabled).ToArray();
                var ordered = ModOrder.EnabledFirst(mods, item => item.Enabled, item => item.BaseGame);
                Require(ReferenceEquals(ordered.Last(), baseGame) && ordered.Length == mods.Count,
                    "Base Game was treated as a normal enabled mod or entries were lost.");
                foreach (bool active in new[] { true, false })
                    Require(ordered.Where(item => !item.BaseGame && item.Enabled == active).SequenceEqual(mods.Where(item => !item.BaseGame && item.Enabled == active)),
                        "Keeping Base Game last changed a normal-mod relative order.");
                Require(mods.Select(item => item.Enabled).SequenceEqual(flags), "Keeping Base Game last changed an enabled state.");
            }
        });
    }
    private sealed class Entry
    {
        internal bool Enabled;
        internal bool BaseGame { get; }
        internal Entry(bool enabled, bool baseGame = false) { Enabled = enabled; BaseGame = baseGame; }
    }
}
