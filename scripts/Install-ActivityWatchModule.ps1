[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $SourceExecutable,

    [string] $ActivityWatchDirectory,

    [string] $AwQtConfigPath =
        "$env:LOCALAPPDATA\activitywatch\activitywatch\aw-qt\aw-qt.toml",

    [string] $OverlayConfigPath =
        "$env:LOCALAPPDATA\activitywatch\CategoryOverlay\config.json",

    [switch] $SkipRegistryMigration
)

$ErrorActionPreference = "Stop"
$ModuleName = "aw-category-overlay"

function Get-UpdatedModuleConfig {
    param(
        [Parameter(Mandatory)]
        [string] $RawConfig,

        [Parameter(Mandatory)]
        [string] $Module,

        [Parameter(Mandatory)]
        [bool] $Install
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
    if ($Install) {
        $items += $Module
    }

    $replacement = "autostart_modules = [" +
        (($items | ForEach-Object { "`"$_`"" }) -join ", ") +
        "]"
    $settingIndex = $section.Groups["body"].Index + $setting.Index

    return $RawConfig.Remove($settingIndex, $setting.Length).
        Insert($settingIndex, $replacement)
}

if (-not (Test-Path $SourceExecutable -PathType Leaf)) {
    throw "Overlay executable not found: $SourceExecutable"
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
if (-not (Test-Path $ActivityWatchDirectory -PathType Container)) {
    throw "ActivityWatch directory not found: $ActivityWatchDirectory"
}

$destination = Join-Path $ActivityWatchDirectory "$ModuleName.exe"
$unexpectedOwner = Get-Process -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Path -eq $destination -and
        $_.ProcessName -ne $ModuleName
    } |
    Select-Object -First 1
if ($unexpectedOwner) {
    throw "Refusing to overwrite $destination while process $($unexpectedOwner.Id) owns it."
}

$rawConfig = [IO.File]::ReadAllText($AwQtConfigPath)
$updatedConfig = Get-UpdatedModuleConfig `
    -RawConfig $rawConfig `
    -Module $ModuleName `
    -Install $true

$updatedOverlayConfig = $null
if (Test-Path $OverlayConfigPath -PathType Leaf) {
    $overlayConfig = [IO.File]::ReadAllText($OverlayConfigPath) |
        ConvertFrom-Json
    if ($null -eq $overlayConfig.startWithWindows) {
        $overlayConfig |
            Add-Member -NotePropertyName startWithWindows `
                -NotePropertyValue $false
    }
    else {
        $overlayConfig.startWithWindows = $false
    }
    $updatedOverlayConfig = $overlayConfig | ConvertTo-Json -Depth 10
}

if ($PSCmdlet.ShouldProcess(
        $ActivityWatchDirectory,
        "Install ActivityWatch category overlay module")) {
    $backupStamp = Get-Date -Format "yyyyMMddHHmmssfff"
    $backupPath = "$AwQtConfigPath.backup-$backupStamp"
    Copy-Item $AwQtConfigPath $backupPath
    Copy-Item $SourceExecutable $destination -Force
    [IO.File]::WriteAllText(
        $AwQtConfigPath,
        $updatedConfig,
        [Text.UTF8Encoding]::new($false))

    if ($null -ne $updatedOverlayConfig) {
        Copy-Item $OverlayConfigPath "$OverlayConfigPath.backup-$backupStamp"
        [IO.File]::WriteAllText(
            $OverlayConfigPath,
            $updatedOverlayConfig,
            [Text.UTF8Encoding]::new($false))
    }

    if (-not $SkipRegistryMigration) {
        Remove-ItemProperty `
            "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
            -Name "ActivityWatchCategoryOverlay" `
            -ErrorAction SilentlyContinue
    }
}
