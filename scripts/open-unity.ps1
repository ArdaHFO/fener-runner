#requires -Version 5.1
<## Open the checked-out game in the native Windows Unity Editor. ##>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityEditor
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

$unityArguments = @('-projectPath', ('"' + $projectPath + '"'), '-force-d3d11')
if ($PSCmdlet.ShouldProcess($projectPath, ('Open Unity Editor: ' + ($unityArguments -join ' ')))) {
    # This is an interactive Editor window requested by the developer.
    Start-Process -FilePath $editorPath -ArgumentList $unityArguments -WorkingDirectory $projectPath
}
