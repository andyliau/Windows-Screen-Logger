#Requires -Modules Pester
<#
.SYNOPSIS
    Pester tests for Summarise-ActivityLog.ps1
#>

BeforeAll {
    $script:scriptPath = Join-Path $PSScriptRoot '..' 'Summarise-ActivityLog.ps1'

    function script:New-TempLog {
        param(
            [string[]]$Lines,
            [string]$Name = 'test.log'
        )
        $path = Join-Path $TestDrive $Name
        [System.IO.File]::WriteAllLines($path, $Lines, [System.Text.Encoding]::UTF8)
        return $path
    }
}

Describe 'Summarise-ActivityLog' {

    Context 'Basic duration calculation' {

        It 'Single record with no dots = 1 x interval' {
            $logPath = script:New-TempLog -Name '2024-01-16.log' -Lines @(
                '10:00:00 notepad "readme.txt"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $winStart = [array]::IndexOf($output, '-- Per Window --')
            $winEnd   = [array]::IndexOf($output, '-- Per Process --')
            $windowLine = $output[($winStart + 1)..($winEnd - 1)] | Where-Object { $_ -match 'notepad' }
            $windowLine | Should -Match '\[0h 00m 05s\]'
        }

        It 'Record followed by N dots = (N+1) x interval' {
            $logPath = script:New-TempLog -Name '2024-01-15.log' -Lines @(
                '09:00:00 code "auth.ts"',
                '.',
                '.',
                '09:00:15 chrome "GitHub"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $winStart = [array]::IndexOf($output, '-- Per Window --')
            $winEnd   = [array]::IndexOf($output, '-- Per Process --')
            $windowLines = $output[($winStart + 1)..($winEnd - 1)]
            # code: 1 record + 2 dots = 3 ticks × 5s = 15s
            $codeLine = $windowLines | Where-Object { $_ -match 'code.*auth\.ts' }
            $codeLine | Should -Match '\[0h 00m 15s\]'
            # chrome: 1 tick × 5s = 5s
            $chromeLine = $windowLines | Where-Object { $_ -match 'chrome' }
            $chromeLine | Should -Match '\[0h 00m 05s\]'
        }

        It 'Respects custom SampleIntervalSeconds' {
            $logPath = script:New-TempLog -Name '2024-01-17.log' -Lines @(
                '08:00:00 code "file.cs"',
                '.'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 10
            $winStart = [array]::IndexOf($output, '-- Per Window --')
            $winEnd   = [array]::IndexOf($output, '-- Per Process --')
            $windowLines = $output[($winStart + 1)..($winEnd - 1)]
            # 2 ticks × 10s = 20s
            $codeLine = $windowLines | Where-Object { $_ -match 'code' }
            $codeLine | Should -Match '\[0h 00m 20s\]'
        }

        It 'Reports correct total active duration' {
            $logPath = script:New-TempLog -Name '2024-01-21.log' -Lines @(
                '09:00:00 code "a"',
                '.', '.',           # 3 ticks = 15s
                '09:00:15 chrome "b"',
                '.'                 # 2 ticks = 10s  → total 25s
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $totalLine = $output | Where-Object { $_ -match 'Total active' }
            $totalLine | Should -Match '0h 00m 25s'
        }

        It 'Sample interval is shown in header' {
            $logPath = script:New-TempLog -Name 'interval-header.log' -Lines @(
                '09:00:00 code "x"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 7
            $output | Should -Contain 'Sample interval : 7s'
        }
    }

    Context 'Grouping by process' {

        It 'Sums durations for the same process across multiple windows' {
            $logPath = script:New-TempLog -Name '2024-01-18.log' -Lines @(
                '09:00:00 code "file1.cs"',
                '.',                        # code: 2 ticks × 5s = 10s
                '09:00:10 chrome "Google"',
                '.',                        # chrome: 2 ticks × 5s = 10s
                '09:00:20 code "file2.cs"',
                '.'                         # code: 2 ticks × 5s = 10s → total code 20s
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $idx = [array]::IndexOf($output, '-- Per Process --')
            $processLines = $output[($idx + 1)..($output.Length - 1)]
            $codeLine = $processLines | Where-Object { $_ -match '\bcode\b' }
            $codeLine | Should -Match '0h 00m 20s'
        }

        It 'Sorts processes by total duration descending' {
            $logPath = script:New-TempLog -Name '2024-01-19.log' -Lines @(
                '09:00:00 chrome "web"',     # chrome: 1 tick = 5s
                '09:00:05 code "file.cs"',
                '.', '.', '.', '.', '.', '.', '.', '.', '.'  # code: 10 ticks = 50s
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $idx = [array]::IndexOf($output, '-- Per Process --')
            $processLines = $output[($idx + 1)..($output.Length - 1)] |
                Where-Object { $_ -match '\S' }
            # code (50s) should appear before chrome (5s)
            $processLines[0] | Should -Match '\bcode\b'
        }

        It 'Per-process section lists all distinct processes' {
            $logPath = script:New-TempLog -Name 'processes.log' -Lines @(
                '09:00:00 code "a"',
                '09:00:05 chrome "b"',
                '09:00:10 msedge "c"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $idx = [array]::IndexOf($output, '-- Per Process --')
            $processLines = $output[($idx + 1)..($output.Length - 1)] |
                Where-Object { $_ -match '\S' }
            $processLines.Count | Should -Be 3
        }
    }

    Context 'Multi-window day' {

        It 'Includes a window entry for every record in the log' {
            $logPath = script:New-TempLog -Name '2024-01-20.log' -Lines @(
                '09:00:00 code "file1.cs"',
                '09:00:05 chrome "GitHub"',
                '.',
                '09:00:15 msedge "Jira"',
                '09:00:20 code "file2.cs"',
                '.', '.'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            ($output | Where-Object { $_ -match 'code.*file1' }).Count  | Should -Be 1
            ($output | Where-Object { $_ -match 'chrome.*GitHub' }).Count | Should -Be 1
            ($output | Where-Object { $_ -match 'msedge.*Jira' }).Count  | Should -Be 1
            ($output | Where-Object { $_ -match 'code.*file2' }).Count  | Should -Be 1
        }

        It 'Shows the log date in the header' {
            $logPath = script:New-TempLog -Name '2024-06-01.log' -Lines @(
                '09:00:00 code "x"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $output[0] | Should -Match '2024-06-01'
        }

        It 'Preserves window ordering chronologically' {
            $logPath = script:New-TempLog -Name 'ordered.log' -Lines @(
                '08:00:00 code "first"',
                '09:00:00 chrome "second"',
                '10:00:00 msedge "third"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $windowIdx = [array]::IndexOf($output, '-- Per Window --')
            $windowLines = $output[($windowIdx + 1)..($output.Length - 1)] |
                Where-Object { $_ -match '\d{2}:\d{2}:\d{2}' }
            $windowLines[0] | Should -Match '08:00:00'
            $windowLines[1] | Should -Match '09:00:00'
            $windowLines[2] | Should -Match '10:00:00'
        }
    }

    Context 'Edge cases' {

        It 'Handles an empty file gracefully' {
            $logPath = script:New-TempLog -Name 'empty.log' -Lines @()
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $output | Should -Contain 'No activity records found.'
        }

        It 'Handles a file with only dots after a single record' {
            $logPath = script:New-TempLog -Name 'single-window.log' -Lines @(
                '14:00:00 code "all-day.cs"',
                '.', '.', '.', '.', '.', '.', '.', '.', '.', '.', '.', '.'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            # 1 record + 12 dots = 13 ticks × 5s = 65s = 1m 05s
            $codeLine = $output | Where-Object { $_ -match 'code' } | Select-Object -First 1
            $codeLine | Should -Match '\[0h 01m 05s\]'
        }

        It 'Throws when the file does not exist' {
            { & $script:scriptPath -Path 'C:\this\does\not\exist.log' } | Should -Throw
        }

        It 'Handles timestamps at the midnight boundary (23:59:xx)' {
            $logPath = script:New-TempLog -Name '2024-01-22.log' -Lines @(
                '23:59:50 code "late-night.cs"',
                '.'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            # 2 ticks × 5s = 10s; timestamp must parse without error
            $winStart = [array]::IndexOf($output, '-- Per Window --')
            $winEnd   = [array]::IndexOf($output, '-- Per Process --')
            $windowLines = $output[($winStart + 1)..($winEnd - 1)]
            $codeLine = $windowLines | Where-Object { $_ -match 'code' }
            $codeLine | Should -Match '23:59:50'
            $codeLine | Should -Match '\[0h 00m 10s\]'
        }

        It 'Silently ignores blank lines embedded in the log' {
            $logPath = script:New-TempLog -Name 'with-blanks.log' -Lines @(
                '09:00:00 code "file.cs"',
                '',
                '.',
                ''
            )
            { & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5 } | Should -Not -Throw
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            # 1 record + 1 dot = 2 ticks × 5s = 10s (blank lines not counted as dots)
            $winStart = [array]::IndexOf($output, '-- Per Window --')
            $winEnd   = [array]::IndexOf($output, '-- Per Process --')
            $windowLines = $output[($winStart + 1)..($winEnd - 1)]
            $codeLine = $windowLines | Where-Object { $_ -match 'code' }
            $codeLine | Should -Match '\[0h 00m 10s\]'
        }

        It 'Handles window titles containing double-quote-like characters in the content' {
            $logPath = script:New-TempLog -Name 'special-title.log' -Lines @(
                '09:00:00 code "main.cs — VS Code"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            ($output | Where-Object { $_ -match 'main\.cs' }).Count | Should -Be 1
        }

        It 'Handles a log where all windows belong to one process' {
            $logPath = script:New-TempLog -Name 'one-proc.log' -Lines @(
                '09:00:00 code "a.cs"',
                '.',
                '09:00:10 code "b.cs"',
                '.',
                '09:00:20 code "c.cs"'
            )
            $output = & $script:scriptPath -Path $logPath -SampleIntervalSeconds 5
            $idx = [array]::IndexOf($output, '-- Per Process --')
            $processLines = @($output[($idx + 1)..($output.Length - 1)] |
                Where-Object { $_ -match '\S' })
            # Only one process entry
            $processLines.Count | Should -Be 1
            # a.cs: 2 ticks = 10s, b.cs: 2 ticks = 10s, c.cs: 1 tick = 5s → total 25s
            $processLines[0] | Should -Match '0h 00m 25s'
        }
    }
}
