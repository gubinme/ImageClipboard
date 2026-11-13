$ErrorActionPreference = "Stop"

try {
    $root = Split-Path -Parent $PSScriptRoot
    $project = Join-Path $root "ImageClipboard\ImageClipboard.csproj"

    Write-Host "1/3 Publish..." -ForegroundColor Cyan
    Get-Process ImageClipboard -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping ImageClipboard (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Milliseconds 400

    dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish завершился с ошибкой ($LASTEXITCODE)."
    }

    $iconPath = Join-Path $root "ImageClipboard\Assets\app.ico"
    if (-not (Test-Path $iconPath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $iconPath) | Out-Null
        dotnet run --project $project --no-build -- --generate-icon $iconPath | Out-Null
    }

    $innoCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )

    $iscc = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        Write-Host ""
        Write-Host "Inno Setup 6 не найден." -ForegroundColor Yellow
        Write-Host "Это нормально: setup.exe собирается через Inno Setup."
        Write-Host ""
        Write-Host "Варианты:"
        Write-Host "  1) Установить Inno Setup: winget install JRSoftware.InnoSetup"
        Write-Host "  2) Или сразу поставить приложение без setup.exe:"
        Write-Host "     .\installer\Install-ImageClipboard.ps1"
        throw "Не найден ISCC.exe"
    }

    $outputDir = Join-Path $PSScriptRoot "output"
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    Write-Host "2/3 Build installer ($iscc)..." -ForegroundColor Cyan
    & $iscc (Join-Path $PSScriptRoot "ImageClipboard.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC завершился с ошибкой ($LASTEXITCODE)."
    }

    Write-Host "3/3 Готово" -ForegroundColor Cyan
    $setup = Get-ChildItem $outputDir -Filter "ImageClipboardSetup-*.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $setup) {
        throw "Файл установщика не создан в $outputDir"
    }

    Write-Host "Setup: $($setup.FullName)" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "Ошибка: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Write-Host ""
    Read-Host "Нажмите Enter, чтобы закрыть"
}
