param([switch]$SkipDownload, [switch]$Sounds)

$ErrorActionPreference = 'Stop'
$tools = $PSScriptRoot
$root = Split-Path -Parent $tools
$godotDir = Join-Path $tools 'godot'
$godot = Join-Path $godotDir 'Godot_v4.7.2-stable_win64_console.exe'

if (-not (Test-Path $godot)) {
	if ($SkipDownload) { throw "Godot 4.7.2 not found at $godot" }
	New-Item -ItemType Directory -Force -Path $godotDir | Out-Null
	$zip = Join-Path $godotDir 'godot.zip'
	Write-Host 'downloading Godot 4.7.2'
	Invoke-WebRequest -Uri 'https://github.com/godotengine/godot/releases/download/4.7.2-stable/Godot_v4.7.2-stable_win64.exe.zip' -OutFile $zip -UseBasicParsing
	Expand-Archive -Path $zip -DestinationPath $godotDir -Force
	Remove-Item $zip -Force
}

$check = Join-Path $tools 'check'
$tests = Join-Path $tools 'tests'
New-Item -ItemType Directory -Force -Path (Join-Path $check 'mod') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $tests 'mod') | Out-Null
Copy-Item (Join-Path $root 'mod\cicada_trainer.gd') (Join-Path $check 'mod\cicada_trainer.gd') -Force
$testScript = [IO.File]::ReadAllText((Join-Path $root 'mod\cicada_trainer.gd')).Replace('user://', 'res://userdata/')
New-Item -ItemType Directory -Force -Path (Join-Path $tests 'userdata') | Out-Null
[IO.File]::WriteAllText((Join-Path $tests 'mod\cicada_trainer.gd'), $testScript)
Copy-Item (Join-Path $root 'trainer\assets\bitpop.otf') (Join-Path $tests 'userdata\cicada_trainer_font.otf') -Force

Write-Host 'parsing mod script'
& $godot --headless --path $check --check-only --script res://mod/cicada_trainer.gd
if ($LASTEXITCODE -ne 0) { throw "the mod script does not parse (exit $LASTEXITCODE)" }

Write-Host 'running headless behaviour tests'
$log = Join-Path $tests 'last-run.log'
$strict = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $godot --headless --path $tests --quit-after 1200 > $log 2>&1
$ErrorActionPreference = $strict
$results = Select-String -Path $log -Pattern 'PASS|FAIL|checks=|TEST OK|TEST FAILED'
$results | ForEach-Object { Write-Host $_.Line }
if (-not ($results | Where-Object { $_.Line -match 'TEST OK' })) { throw 'behaviour tests failed, see tools/tests/last-run.log' }
Write-Host 'all checks passed'

Write-Host 'checking that every key name the trainer can bind is understood by Godot'
$formSource = Get-Content (Join-Path $root 'trainer\TrainerForm.cs') -Raw
$literalKeys = [regex]::Matches($formSource, '=>\s*"([A-Za-z0-9 ]+)"') | ForEach-Object { $_.Groups[1].Value }
$keyNames = @()
$keyNames += 1..12 | ForEach-Object { "F$_" }
$keyNames += [char[]]([char]'A'..[char]'Z') | ForEach-Object { "$_" }
$keyNames += 0..9 | ForEach-Object { "$_" }
$keyNames += $literalKeys
$keyList = ($keyNames | Select-Object -Unique) -join ','
$keyLog = Join-Path $tests 'last-keys.log'
& $godot --headless --path $tests --script res://check_keys.gd -- $keyList > $keyLog 2>&1
Select-String -Path $keyLog -Pattern 'checked|KEYS' | ForEach-Object { Write-Host $_.Line }
if (-not (Select-String -Path $keyLog -Pattern 'KEYS OK' -Quiet)) { throw 'some key names cannot be parsed by Godot, see tools/tests/last-keys.log' }

if ($Sounds) {
	$python = (Get-Command python -ErrorAction SilentlyContinue).Source
	if (-not $python) { $python = (Get-Command py -ErrorAction SilentlyContinue).Source }
	if (-not $python) { Write-Warning 'python not found, skipping the sound comparison'; exit 0 }
	Write-Host 'comparing the sounds synthesized in Godot with the WAV previews of the exe'
	$dumpLog = Join-Path $tests 'last-sounds.log'
	& $godot --headless --path $tests --script res://dump_sounds.gd > $dumpLog 2>&1
	$dumped = Join-Path $env:APPDATA 'Godot\app_userdata\cicada-trainer-tests'
	& $python (Join-Path $tools 'compare_sounds.py') $dumped (Join-Path $root 'trainer\assets\sounds')
	if ($LASTEXITCODE -ne 0) { throw 'the in-game sounds differ from the exe previews' }
}
