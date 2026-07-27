[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ActivityWatchDirectory,

    [string] $AwQtConfigPath =
        "$env:LOCALAPPDATA\activitywatch\activitywatch\aw-qt\aw-qt.toml"
)

$ErrorActionPreference = "Stop"
$ModuleName = "aw-category-overlay"

function Get-ConfigWithoutModule {
    param(
        [Parameter(Mandatory)]
        [string] $RawConfig,

        [Parameter(Mandatory)]
        [string] $Module
    )

    $section = [regex]::Match(
        $RawConfig,
        '(?ms)^\[aw-qt\]\s*\r?\n(?<body>.*?)(?=^\[|\z)')
    if (-not $section.Success) {
        throw "Could not find [aw-qt] in the ActivityWatch launcher config."
    }

    $setting = [regex]::Match(
        $section.Groups["body"].Value,
        '(?m)^(?:#\s*)?autostart_modules\s*=\s*\[(?<items>[^\]]*)\]')
    if (-not $setting.Success) {
        throw "Could not find aw-qt autostart_modules in the production section."
    }

    $items = @(
        [regex]::Matches($setting.Groups["items"].Value, '"([^"]+)"') |
            ForEach-Object { $_.Groups[1].Value } |
            Where-Object { $_ -ne $Module }
    )
    $replacement = "autostart_modules = [" +
        (($items | ForEach-Object { "`"$_`"" }) -join ", ") +
        "]"
    $settingIndex = $section.Groups["body"].Index + $setting.Index

    return $RawConfig.Remove($settingIndex, $setting.Length).
        Insert($settingIndex, $replacement)
}

if (-not (Test-Path $AwQtConfigPath -PathType Leaf)) {
    throw "aw-qt config not found: $AwQtConfigPath"
}
if (-not $ActivityWatchDirectory) {
    $awQt = Get-Process aw-qt -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $awQt -or -not $awQt.Path) {
        throw "aw-qt is not running; pass -ActivityWatchDirectory explicitly."
    }
    $ActivityWatchDirectory = Split-Path $awQt.Path
}

$destination = Join-Path $ActivityWatchDirectory "$ModuleName.exe"
$rawConfig = [IO.File]::ReadAllText($AwQtConfigPath)
$updatedConfig = Get-ConfigWithoutModule `
    -RawConfig $rawConfig `
    -Module $ModuleName

if ($PSCmdlet.ShouldProcess(
        $ActivityWatchDirectory,
        "Uninstall ActivityWatch category overlay module")) {
    $backupPath = "$AwQtConfigPath.backup-" +
        (Get-Date -Format "yyyyMMddHHmmssfff")
    Copy-Item $AwQtConfigPath $backupPath
    [IO.File]::WriteAllText(
        $AwQtConfigPath,
        $updatedConfig,
        [Text.UTF8Encoding]::new($false))
    if (Test-Path $destination -PathType Leaf) {
        Remove-Item $destination
    }
}
