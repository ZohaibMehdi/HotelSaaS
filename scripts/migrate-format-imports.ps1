# One-time import migration: split fmt* helpers out of components/ui into lib/format
$ErrorActionPreference = "Continue"
$root = Join-Path $PSScriptRoot "..\frontend\src"

Get-ChildItem -Path $root -Recurse -Include *.tsx | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($null -eq $c -or $c -notmatch 'fmt(Money|Date|DateTime)') { return }

    $needed = @()
    foreach ($n in @('fmtMoney','fmtDate','fmtDateTime')) {
        if ($c -match [regex]::Escape($n)) { $needed += $n }
    }
    if ($needed.Count -eq 0) { return }
    $fmtImport = "import { " + ($needed -join ', ') + ' } from "@/lib/format";'

    # Case 1: ui import already uses "@/components/ui" (post-alias style)
    $pattern1 = 'import \{([^}]*)\} from "@/components/ui";'
    if ($c -match $pattern1) {
        $names = ($Matches[1] -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notin $needed }
        $newUi = if ($names.Count -gt 0) { "import { " + ($names -join ', ') + ' } from "@/components/ui";' } else { "" }
        $c = $c -replace $pattern1, $newUi
        # insert format import after the (possibly emptied) ui import line
        $c = $c -replace '(\r?\n)(import|\r?\n)', "`$1$fmtImport`$1`$2", 1
        if ($c -notmatch 'lib/format') {
            $c = $fmtImport + "`r`n" + $c
        }
    }
    else {
        # Case 2: relative "../components/ui" imports, possibly multiple lines
        $lines = $c -split "`r`n"
        $out = New-Object System.Collections.Generic.List[string]
        $inserted = $false
        foreach ($line in $lines) {
            if ($line -match '^import \{([^}]*)\} from "\.\./components/ui";') {
                $names = ($Matches[1] -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notin $needed }
                if ($names.Count -gt 0) { $out.Add("import { " + ($names -join ', ') + ' } from "../components/ui";') }
                if (-not $inserted) { $out.Add($fmtImport); $inserted = $true }
            }
            else {
                $out.Add($line)
            }
        }
        if (-not $inserted) { $out.Insert(0, $fmtImport) }
        $c = ($out -join "`r`n")
    }

    Set-Content -Path $_.FullName -Value $c -NoNewline
    Write-Host ("migrated: " + $_.Name + " -> " + ($needed -join ','))
}
Write-Host "done"
