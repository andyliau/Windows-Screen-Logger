#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Summarises a Windows Screen Logger activity log into human-readable text for AI consumption.
.DESCRIPTION
    Reads a daily activity log produced by ActivityLoggingService (YYYY-MM-DD.log format),
    calculates focus duration per window and per process, then writes a structured plain-text
    summary to stdout. Output is suitable for piping directly to an AI summarisation tool.

    Log format:
      HH:mm:ss proc "title"   — window-change record
      .                        — same window still active (one sample tick)

    Duration rule: a window record followed by N dots = (N+1) × SampleIntervalSeconds.
.PARAMETER Path
    Path to the .log file to process.
.PARAMETER SampleIntervalSeconds
    Duration in seconds of each sample tick (default: 5, minimum: 1).
.EXAMPLE
    .\Summarise-ActivityLog.ps1 -Path "$env:USERPROFILE\WindowsScreenLogger\2024-01-15.log"
.EXAMPLE
    .\Summarise-ActivityLog.ps1 -Path "2024-01-15.log" -SampleIntervalSeconds 10 | clip
.OUTPUTS
    [string] Formatted activity summary written to the success stream (stdout).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)]
    [string]$Path,

    [Parameter()]
    [ValidateRange(1, 3600)]
    [int]$SampleIntervalSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Returns a list of raw parsed records: Timestamp (TimeSpan), Process, Title, DotCount
function Read-ActivityLog {
    param([string]$LiteralPath)

    $lines = [System.IO.File]::ReadAllLines($LiteralPath, [System.Text.Encoding]::UTF8)
    $records = [System.Collections.Generic.List[PSCustomObject]]::new()
    $windowPattern = '^(\d{2}:\d{2}:\d{2})\s+(\S+)\s+"(.*)"$'

    $current = $null
    $dotCount = 0

    foreach ($line in $lines) {
        if ($line -match $windowPattern) {
            if ($null -ne $current) {
                $current.DotCount = $dotCount
                $records.Add($current)
            }
            $current = [PSCustomObject]@{
                Timestamp = [TimeSpan]::Parse($Matches[1])
                Process   = $Matches[2]
                Title     = $Matches[3]
                DotCount  = 0
            }
            $dotCount = 0
        }
        elseif ($line -eq '.') {
            $dotCount++
        }
        # blank lines and unrecognised lines are silently skipped
    }

    if ($null -ne $current) {
        $current.DotCount = $dotCount
        $records.Add($current)
    }

    return $records
}

# Converts raw records into window entries with DurationSeconds, plus a per-process rollup
function Measure-ActivitySummary {
    param(
        [System.Collections.Generic.List[PSCustomObject]]$Records,
        [int]$IntervalSeconds
    )

    $windows = [System.Collections.Generic.List[PSCustomObject]]::new()

    foreach ($record in $Records) {
        $windows.Add([PSCustomObject]@{
            Timestamp       = $record.Timestamp
            Process         = $record.Process
            Title           = $record.Title
            DurationSeconds = ($record.DotCount + 1) * $IntervalSeconds
        })
    }

    $byProcess = $windows |
        Group-Object -Property Process |
        ForEach-Object {
            [PSCustomObject]@{
                Process         = $_.Name
                DurationSeconds = ($_.Group | Measure-Object -Property DurationSeconds -Sum).Sum
            }
        } |
        Sort-Object -Property DurationSeconds -Descending

    return [PSCustomObject]@{
        Windows   = $windows
        ByProcess = $byProcess
    }
}

function Format-Duration {
    param([int]$TotalSeconds)
    $h = [int][Math]::Floor($TotalSeconds / 3600)
    $m = [int][Math]::Floor(($TotalSeconds % 3600) / 60)
    $s = [int]($TotalSeconds % 60)
    return '{0}h {1:D2}m {2:D2}s' -f $h, $m, $s
}

function Format-ActivitySummary {
    param(
        [PSCustomObject]$Summary,
        [string]$LogDate,
        [int]$IntervalSeconds
    )

    $output = [System.Collections.Generic.List[string]]::new()
    $totalSeconds = ($Summary.Windows | Measure-Object -Property DurationSeconds -Sum).Sum

    $output.Add("=== Activity Summary: $LogDate ===")
    $output.Add("Sample interval : ${IntervalSeconds}s")
    $output.Add("Total active    : $(Format-Duration -TotalSeconds $totalSeconds)")
    $output.Add('')
    $output.Add('-- Per Window --')

    foreach ($w in $Summary.Windows) {
        $ts  = '{0:hh\:mm\:ss}' -f $w.Timestamp
        $dur = Format-Duration -TotalSeconds $w.DurationSeconds
        $output.Add("  $ts  $($w.Process.PadRight(20))  `"$($w.Title)`"  [$dur]")
    }

    $output.Add('')
    $output.Add('-- Per Process --')

    foreach ($p in $Summary.ByProcess) {
        $dur = Format-Duration -TotalSeconds $p.DurationSeconds
        $output.Add("  $($p.Process.PadRight(20))  $dur")
    }

    return $output
}

function Main {
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Log file not found: $resolvedPath"
    }

    $logDate = [System.IO.Path]::GetFileNameWithoutExtension($resolvedPath)
    $records = Read-ActivityLog -LiteralPath $resolvedPath

    if ($null -eq $records -or $records.Count -eq 0) {
        Write-Output "=== Activity Summary: $logDate ==="
        Write-Output "No activity records found."
        return
    }

    $summary = Measure-ActivitySummary -Records $records -IntervalSeconds $SampleIntervalSeconds
    foreach ($line in (Format-ActivitySummary -Summary $summary -LogDate $logDate -IntervalSeconds $SampleIntervalSeconds)) {
        Write-Output $line
    }
}

if ($MyInvocation.InvocationName -ne '.') { Main }
