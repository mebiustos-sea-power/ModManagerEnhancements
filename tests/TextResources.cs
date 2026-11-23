using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using ModManagerEnhancements.Core;

internal static class TestText
{
    internal static string FileFor(string language) => Path.Combine(AppContext.BaseDirectory,
        "language_" + language, "mod_manager_enhancements.json");

    internal static ModText Create(string language)
    {
        var english = Read(FileFor("en"));
        var selected = File.Exists(FileFor(language)) ? Read(FileFor(language)) : english;
        return new ModText(key => (selected.TryGetValue(key, out string? value) ? value : english[key])
            .Replace("\\n", "\n").Replace("\\r", "\r"));
    }

    // Core tests read the shipped files; native UI tests separately exercise Sea Power's actual loader.
    private static Dictionary<string, string> Read(string path)
    {
        var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, Dictionary<string, string>>),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        using var input = File.OpenRead(path);
        return ((Dictionary<string, Dictionary<string, string>>)serializer.ReadObject(input)!)[ModText.Section];
    }
}
