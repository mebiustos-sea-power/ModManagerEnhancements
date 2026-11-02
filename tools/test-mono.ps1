param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskManaged = Join-Path $GameDirectory 'Sea Power_Data\Managed'
$taskRuntime = Join-Path $GameDirectory 'MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll'
$taskTests = Join-Path $taskRoot 'tests\bin\Release\netstandard2.1\ModManagerEnhancements.Tests.dll'
foreach ($taskPath in @($taskRuntime, $taskTests)) { if (-not (Test-Path $taskPath)) { throw "Missing Mono dependency: $taskPath" } }
$taskNative = @"
using System;
using System.Runtime.InteropServices;
public static class ModManagerEnhancementsTestMono {
    [DllImport(@"$taskRuntime", CallingConvention=CallingConvention.Cdecl)]
    public static extern void mono_set_assemblies_path(string path);
    [DllImport(@"$taskRuntime", CallingConvention=CallingConvention.Cdecl)]
    public static extern void mono_set_dirs(string assemblyPath, string configPath);
    [DllImport(@"$taskRuntime", CallingConvention=CallingConvention.Cdecl)]
    public static extern int mono_main(int argc, IntPtr argv);
}
"@
Add-Type -TypeDefinition $taskNative
$taskSearch = @($taskManaged, (Split-Path $taskTests -Parent)) -join ';'
[Environment]::SetEnvironmentVariable('MONO_PATH', $taskSearch)
[ModManagerEnhancementsTestMono]::mono_set_dirs($taskManaged, (Join-Path $GameDirectory 'MonoBleedingEdge\etc'))
[ModManagerEnhancementsTestMono]::mono_set_assemblies_path($taskSearch)
$taskArguments = @('ModManagerEnhancementsTests', '--runtime=v4.0.30319', $taskTests)
$taskStrings = @($taskArguments | ForEach-Object { [Runtime.InteropServices.Marshal]::StringToHGlobalAnsi($_) })
$taskVector = [Runtime.InteropServices.Marshal]::AllocHGlobal([IntPtr]::Size * ($taskStrings.Count + 1))
try {
    for ($taskIndex=0; $taskIndex -lt $taskStrings.Count; $taskIndex++) {
        [Runtime.InteropServices.Marshal]::WriteIntPtr($taskVector, [IntPtr]::Size * $taskIndex, $taskStrings[$taskIndex])
    }
    [Runtime.InteropServices.Marshal]::WriteIntPtr($taskVector, [IntPtr]::Size * $taskStrings.Count, [IntPtr]::Zero)
    Write-Output 'Running functional checks in a separate game-Mono process; Sea Power is not started or attached.'
    $taskResult = [ModManagerEnhancementsTestMono]::mono_main($taskStrings.Count, $taskVector)
    if ($taskResult -ne 0) { throw "Mono checks failed: $taskResult" }
} finally {
    foreach ($taskPointer in $taskStrings) { [Runtime.InteropServices.Marshal]::FreeHGlobal($taskPointer) }
    [Runtime.InteropServices.Marshal]::FreeHGlobal($taskVector)
}
