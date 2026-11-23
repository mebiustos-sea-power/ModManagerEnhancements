using System;
using ModManagerEnhancements.Core;
using SeaPower;

namespace ModManagerEnhancements;

internal static class GameText
{
    internal static ModText Create() => Create(() => LanguageResourceHandler.Instance);
    internal static ModText Create(Func<LanguageResourceHandler> current) =>
        new(key => current().getText(ModText.Section, key));
}
