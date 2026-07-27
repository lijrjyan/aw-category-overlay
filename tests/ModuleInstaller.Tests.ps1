$repositoryRoot = Split-Path $PSScriptRoot -Parent
$installScript = Join-Path $repositoryRoot "scripts\Install-ActivityWatchModule.ps1"
$uninstallScript = Join-Path $repositoryRoot "scripts\Uninstall-ActivityWatchModule.ps1"

Describe "ActivityWatch category overlay module installer" {
    BeforeEach {
        $caseRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString("N"))
        $activityWatchDirectory = Join-Path $caseRoot "ActivityWatch"
        $configDirectory = Join-Path $caseRoot "config"
        New-Item $activityWatchDirectory -ItemType Directory | Out-Null
        New-Item $configDirectory -ItemType Directory | Out-Null

        $sourceExecutable = Join-Path $caseRoot "ActivityWatch.CategoryOverlay.exe"
        Set-Content $sourceExecutable "overlay-binary"

        $awQtConfigPath = Join-Path $configDirectory "aw-qt.toml"
        @"
[aw-qt]
#autostart_modules = ["aw-server", "aw-watcher-afk", "aw-watcher-window"]

[aw-qt-testing]
#autostart_modules = ["aw-server", "aw-watcher-afk", "aw-watcher-window"]
"@ | Set-Content $awQtConfigPath

        $overlayConfigPath = Join-Path $configDirectory "config.json"
        @{
            schemaVersion = 1
            startWithWindows = $true
            refreshMinutes = 5
        } | ConvertTo-Json | Set-Content $overlayConfigPath
    }

    It "installs the executable and enables only the production aw-qt module list" {
        & $installScript `
            -SourceExecutable $sourceExecutable `
            -ActivityWatchDirectory $activityWatchDirectory `
            -AwQtConfigPath $awQtConfigPath `
            -OverlayConfigPath $overlayConfigPath `
            -SkipRegistryMigration

        $installed = Join-Path $activityWatchDirectory "aw-category-overlay.exe"
        (Test-Path $installed) | Should Be $true
        (Get-Content $installed -Raw).Trim() | Should Be "overlay-binary"

        $raw = Get-Content $awQtConfigPath -Raw
        $raw | Should Match '\[aw-qt\]\r?\nautostart_modules = \["aw-server", "aw-watcher-afk", "aw-watcher-window", "aw-category-overlay"\]'
        $raw | Should Match '\[aw-qt-testing\]\r?\n#autostart_modules = \["aw-server", "aw-watcher-afk", "aw-watcher-window"\]'

        (Get-Content $overlayConfigPath -Raw |
            ConvertFrom-Json).startWithWindows | Should Be $false
        @(Get-ChildItem "$awQtConfigPath.backup-*").Count | Should Be 1
    }

    It "is idempotent and keeps one overlay module entry" {
        1..2 | ForEach-Object {
            & $installScript `
                -SourceExecutable $sourceExecutable `
                -ActivityWatchDirectory $activityWatchDirectory `
                -AwQtConfigPath $awQtConfigPath `
                -OverlayConfigPath $overlayConfigPath `
                -SkipRegistryMigration
        }

        $raw = Get-Content $awQtConfigPath -Raw
        $productionSection = [regex]::Match(
            $raw,
            '(?ms)^\[aw-qt\]\s*\r?\n(?<body>.*?)(?=^\[|\z)')
        ([regex]::Matches(
            $productionSection.Groups["body"].Value,
            '"aw-category-overlay"')).Count | Should Be 1
    }

    It "uninstalls only the overlay module and executable" {
        & $installScript `
            -SourceExecutable $sourceExecutable `
            -ActivityWatchDirectory $activityWatchDirectory `
            -AwQtConfigPath $awQtConfigPath `
            -OverlayConfigPath $overlayConfigPath `
            -SkipRegistryMigration

        & $uninstallScript `
            -ActivityWatchDirectory $activityWatchDirectory `
            -AwQtConfigPath $awQtConfigPath

        (Test-Path (
            Join-Path $activityWatchDirectory "aw-category-overlay.exe")) |
            Should Be $false
        $raw = Get-Content $awQtConfigPath -Raw
        $raw | Should Match '\[aw-qt\]\r?\nautostart_modules = \["aw-server", "aw-watcher-afk", "aw-watcher-window"\]'
        $raw | Should Match '\[aw-qt-testing\]\r?\n#autostart_modules = \["aw-server", "aw-watcher-afk", "aw-watcher-window"\]'
    }

    It "refuses a config without the production module setting" {
        @"
[aw-qt]
unknown_setting = true
"@ | Set-Content $awQtConfigPath

        {
            & $installScript `
                -SourceExecutable $sourceExecutable `
                -ActivityWatchDirectory $activityWatchDirectory `
                -AwQtConfigPath $awQtConfigPath `
                -OverlayConfigPath $overlayConfigPath `
                -SkipRegistryMigration
        } | Should Throw
    }
}
