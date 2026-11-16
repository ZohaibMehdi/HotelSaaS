# Cleanup pass: strip fmt* from ui imports (incl. multi-line) and de-duplicate lib/format imports
$ErrorActionPreference = "Continue"
$root = Join-Path $PSScriptRoot "..\frontend\src"
$fmtNames = @('fmtMoney','fmtDate','fmtDateTime')

Get-ChildItem -Path $root -Recurse -Include *.tsx | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($null -eq $c) { return }
    $orig = $c

    foreach ($mod in @('../components/ui', '@/components/ui')) {
        $escMod = [regex]::Escape($mod)
        $pattern = 'import \{(?<names>[^}]*)\} from "' + $escMod + '";'
        $c = [regex]::Replace($c, $pattern, {
            param($m)
            $kept = ($m.Groups['names'].Value -split ',') |
                ForEach-Object { $_.Trim() } |
                Where-Object { $_ -and ($fmtNames -notcontains $_) }
            if ($kept.Count -eq 0) { return "" }
            return "import { " + ($kept -join ', ') + ' } from "' + $mod + '";'
        })
    }

    # Merge/dedupe existing lib/format imports into one
    $fmtPattern = 'import \{(?<names>[^}]*)\} from "@/lib/format";'
    $all = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($c, $fmtPattern)) {
        foreach ($n in ($m.Groups['names'].Value -split ',')) {
            $t = $n.Trim()
            if ($t -and -not $all.Contains($t)) { $all.Add($t) | Out-Null }
        }
    }
    if ($all.Count -gt 0) {
        $c = [regex]::Replace($c, $fmtPattern, "")
        $oneImport = "import { " + ($all -join ', ') + ' } from "@/lib/format";'
        # insert after last top import line block start: simply before first export/const/function or after final import
        $idx = $c.LastIndexOf('";')
        if ($idx -ge 0) {
            $lineEnd = $c.IndexOf("`n", $idx)
            $pos = if ($lineEnd -ge 0) { $lineEnd + 1 } else { $c.Length }
            $c = $c.Insert($pos, $oneImport + "`r`n")
        } else {
            $c = $oneImport + "`r`n" + $c
        }
        # tidy possible triple blank lines
        $c = $c -replace "(`r?`n){3,}", "`r`n`r`n"
    }

    if ($c -ne $orig) {
        Set-Content -Path $_.FullName -Value $c -NoNewline
        Write-Host ("cleaned: " + $_.Name)
    }
}
Write-Host "done"
