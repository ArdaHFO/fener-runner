#requires -Version 5.1
<## Build a local Android preview. Never uploads or publishes the result. ##>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityEditor,
    [string]$OutputPath,
    [string]$BuildCacheRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'UnityProject'
$editorPath = [IO.Path]::GetFullPath($UnityEditor)
if (-not (Test-Path -LiteralPath $editorPath -PathType Leaf)) {
    throw 'UnityEditor must point to the installed Unity.exe file.'
}
if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'))) {
    throw 'The Unity project is missing from this checkout.'
}
if (-not $OutputPath) { $OutputPath = 'Builds\Android\FenerRunner-v0.20-arm64.apk' }
if (-not [IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $repositoryRoot $OutputPath }
$apkPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetExtension($apkPath) -ine '.apk') { throw 'OutputPath must end in .apk.' }
if (-not $BuildCacheRoot) { $BuildCacheRoot = Join-Path $repositoryRoot '.local-build' }
if (-not [IO.Path]::IsPathRooted($BuildCacheRoot)) { $BuildCacheRoot = Join-Path $repositoryRoot $BuildCacheRoot }
$cachePath = [IO.Path]::GetFullPath($BuildCacheRoot)
$logPath = Join-Path $cachePath 'android-build.log'
$gradlePath = Join-Path $cachePath 'gradle'
$androidUserPath = Join-Path $cachePath 'android-user'
$javaTemp = (Join-Path $cachePath 'tmp').Replace('\', '/')
$androidTools = Join-Path (Split-Path -Parent $editorPath) 'Data\PlaybackEngines\AndroidPlayer'
$unityArguments = @(
    '-batchmode', '-quit', '-force-d3d11', '-buildTarget', 'Android',
    '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'FenerRunner.Editor.AndroidApkBuilder.Build',
    '-apkOutput', ('"' + $apkPath + '"'), '-logFile', ('"' + $logPath + '"')
)

if (-not $PSCmdlet.ShouldProcess($apkPath, ('Build Android preview: ' + ($unityArguments -join ' ')))) { return }

if (-not (Test-Path -LiteralPath (Join-Path $androidTools 'OpenJDK\bin\java.exe')) -or
    -not (Test-Path -LiteralPath (Join-Path $androidTools 'SDK')) -or
    -not (Test-Path -LiteralPath (Join-Path $androidTools 'NDK'))) {
    throw 'Install Android Build Support, Android SDK/NDK Tools, and OpenJDK for this Unity Editor in Unity Hub.'
}
# Recent Java/Gradle workers use Unix-domain sockets with a short path limit.
# Reserve room for their generated socket filename. Never alter system TEMP.
if ([Text.Encoding]::UTF8.GetByteCount($javaTemp) -gt 75) {
    throw 'The build-cache path is too long for Java worker sockets. Use -BuildCacheRoot with a short writable path, for example C:\fr-build. Keep that directory because it contains your local signing key.'
}
if (Test-Path -LiteralPath (Join-Path $projectPath 'Temp\UnityLockfile')) {
    throw 'Close the Unity Editor for this project before starting the batch build.'
}

$environmentValues = @{
    GRADLE_USER_HOME = $gradlePath
    ANDROID_USER_HOME = $androidUserPath
    JAVA_HOME = (Join-Path $androidTools 'OpenJDK')
    ANDROID_HOME = (Join-Path $androidTools 'SDK')
    ANDROID_SDK_ROOT = (Join-Path $androidTools 'SDK')
    TEMP = $javaTemp
    TMP = $javaTemp
    JAVA_TOOL_OPTIONS = (($env:JAVA_TOOL_OPTIONS + ' "-Djdk.net.unixdomain.tmpdir=' + $javaTemp + '"').Trim())
}
$savedEnvironment = @{}
foreach ($name in $environmentValues.Keys) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    foreach ($directory in @($cachePath, $gradlePath, $androidUserPath, $javaTemp, (Split-Path -Parent $apkPath))) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    foreach ($name in $environmentValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $environmentValues[$name], 'Process')
    }
    Write-Output ('Building Android preview. Log: ' + $logPath)
    $buildStarted = [DateTime]::UtcNow
    $buildProcess = Start-Process -FilePath $editorPath -ArgumentList $unityArguments -WorkingDirectory $projectPath -WindowStyle Hidden -PassThru
    $buildProcess.WaitForExit()
    $buildProcess.Refresh()
    if ($buildProcess.ExitCode -ne 0) {
        throw ('Unity exited with code ' + $buildProcess.ExitCode + '. See ' + $logPath)
    }
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) { throw 'Unity did not create the requested APK.' }
    $builtApk = Get-Item -LiteralPath $apkPath
    if ($builtApk.LastWriteTimeUtc -lt $buildStarted -or $builtApk.Length -eq 0) {
        throw 'The output APK is empty or predates this build. Inspect the Unity log.'
    }
    [PSCustomObject]@{
        Apk = $builtApk.FullName
        Bytes = $builtApk.Length
        SHA256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Log = $logPath
        LocalSigningDirectory = $androidUserPath
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}
