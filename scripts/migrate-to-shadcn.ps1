# One-time migration: purge custom 'brand-*' palette -> shadcn zinc/primary tokens
$ErrorActionPreference = "Continue"
$root = Join-Path $PSScriptRoot "..\frontend\src"

$map = [ordered]@{
  'accent-brand-600'       = 'accent-zinc-900'
  'from-brand-950'         = 'from-zinc-950'
  'via-brand-900'          = 'via-zinc-900'
  'to-slate-900 '          = 'to-zinc-900 '
  'bg-brand-950'           = 'bg-zinc-950'
  'bg-brand-900'           = 'bg-zinc-900'
  'hover:bg-brand-700'     = 'hover:bg-primary/90'
  'bg-brand-700'           = 'bg-primary'
  'bg-brand-600'           = 'bg-primary'
  'hover:bg-brand-500'     = 'hover:bg-zinc-800'
  'bg-brand-500'           = 'bg-zinc-800'
  'bg-brand-50'            = 'bg-muted'
  'text-brand-700'         = 'text-primary'
  'text-brand-600'         = 'text-primary'
  'hover:text-brand-600'   = 'hover:text-primary'
  'border-brand-600'       = 'border-primary'
  'border-brand-400'       = 'border-zinc-400'
  'ring-brand-600'         = 'ring-ring'
}

$btnMap = [ordered]@{
  'btn-secondary !py-1 !px-2 !text-xs'      = 'btn-xs-secondary'
  'btn-danger !py-1 !px-2 !text-xs'         = 'btn-xs-danger'
  'btn-success !py-1 !px-2 !text-xs'        = 'btn-xs-success'
  'btn-secondary !py-1 !px-2 !text-[11px]'  = 'btn-xs-secondary'
  'btn-danger !py-1 !px-2 !text-[11px]'     = 'btn-xs-danger'
  'btn-success !py-1 !px-2 !text-[11px]'    = 'btn-xs-success'
  'btn-primary !py-1 !px-2 !text-[11px]'    = 'btn-xs bg-primary text-white shadow-sm hover:bg-primary/90'
  'btn-secondary !py-1.5 !px-2 !text-xs'    = 'btn-xs-secondary'
  'btn-secondary !py-1.5 !text-xs'          = 'btn-secondary !h-8 !px-3 !text-xs'
  'btn-primary !py-1.5 !text-xs'            = 'btn-primary !h-8 !px-3 !text-xs'
}

Get-ChildItem -Path $root -Recurse -Include *.tsx, *.ts | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    if ($null -eq $c) { return }
    $orig = $c
    foreach ($k in $map.Keys)   { $c = $c.Replace($k, $map[$k]) }
    foreach ($k in $btnMap.Keys){ $c = $c.Replace($k, $btnMap[$k]) }
    # neutralize any leftover focus ring pair from old theme
    $c = $c.Replace('focus:ring-ring focus:border-ring', '')
    if ($c -ne $orig) {
        Set-Content -Path $_.FullName -Value $c -NoNewline
        Write-Host ("updated: " + $_.Name)
    }
}
Write-Host "`nRemaining 'brand-' references:"
(Select-String -Path (Join-Path $root '*\*.tsx') -Pattern 'brand-' -ErrorAction SilentlyContinue |
  Select-String -Path (Join-Path $root '*.tsx') -Pattern 'brand-' -ErrorAction SilentlyContinue) | Out-Null
Get-ChildItem -Path $root -Recurse -Include *.tsx | Select-String -Pattern 'brand-' | ForEach-Object { Write-Host ("  " + $_.Path + ":" + $_.LineNumber) }
Write-Host "done"
