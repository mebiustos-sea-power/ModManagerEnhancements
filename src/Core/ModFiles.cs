using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ModManagerEnhancements.Core;

internal static class ModFiles
{
    internal static IEnumerable<string> Enumerate(DirectoryInfo root, CancellationToken cancellation, bool includeHiddenDirectories = false)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var item in pending.Pop().EnumerateFileSystemInfos())
            {
                cancellation.ThrowIfCancellationRequested();
                if (item is DirectoryInfo directory)
                {
                    if (!includeHiddenDirectories && directory.Name.StartsWith(".", StringComparison.Ordinal)) continue;
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Cannot safely scan a linked subdirectory: " + directory.FullName);
                    pending.Push(directory);
                }
                else if (item is FileInfo file) yield return file.FullName;
            }
        }
    }
}
