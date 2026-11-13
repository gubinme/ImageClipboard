#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$AppName = "ImageClipboard"
$AppExe = "ImageClipboard.exe"
$AppVersion = "1.1.0"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "ImageClipboard\ImageClipboard.csproj"
$PublishDir = Join-Path $Root "ImageClipboard\bin\Release\net8.0-windows\win-x64\publish"
$RegPath = "HKCU:\Software\$AppName"

function Write-Step($text) {
    Write-Host ""
    Write-Host "==> $text" -ForegroundColor Cyan
}

function Get-InstalledInfo {
    if (-not (Test-Path $RegPath)) {
        return $null
    }

    $path = (Get-ItemProperty -Path $RegPath -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $version = (Get-ItemProperty -Path $RegPath -Name DisplayVersion -ErrorAction SilentlyContinue).DisplayVersion
    if (-not $path -and -not $version) {
        return $null
    }

    return [pscustomobject]@{
        Path    = $path
        Version = $version
    }
}

function Compare-Version([string]$Left, [string]$Right) {
    $a = [version](($Left -split '-')[0])
    $b = [version](($Right -split '-')[0])
    return $a.CompareTo($b)
}

function Select-InstallFolder([string]$DefaultPath) {
    Add-Type -AssemblyName System.Windows.Forms | Out-Null
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Выберите папку установки ImageClipboard"
    if (Test-Path $DefaultPath) {
        $dialog.SelectedPath = $DefaultPath
    }
    $dialog.ShowNewFolderButton = $true

    $result = $dialog.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        throw "Установка отменена."
    }

    return $dialog.SelectedPath
}

function Stop-AppIfRunning {
    Get-Process -Name "ImageClipboard" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Закрываю процесс ImageClipboard (PID $($_.Id))..."
        $_.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 700
        if (-not $_.HasExited) {
            Stop-Process -Id $_.Id -Force
        }
    }
}

try {
    Write-Host "ImageClipboard Installer $AppVersion" -ForegroundColor Green

    $installed = Get-InstalledInfo
    if ($installed -and $installed.Path) {
        $defaultDir = $installed.Path
    }
    else {
        $defaultDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
    }

    if ($installed -and $installed.Version) {
        Write-Host "Найдена установленная версия: $($installed.Version)"
        Write-Host "Путь: $($installed.Path)"

        $cmp = Compare-Version $AppVersion $installed.Version
        if ($cmp -lt 0) {
            $answer = Read-Host "Уже стоит более новая версия ($($installed.Version)). Продолжить установку $AppVersion? (y/N)"
            if ($answer -notin @("y", "Y")) {
                throw "Установка отменена."
            }
        }
        elseif ($cmp -eq 0) {
            Write-Host "Будет переустановка той же версии $AppVersion"
        }
        else {
            Write-Host "Будет обновление $($installed.Version) -> $AppVersion"
        }
    }

    Write-Step "Выбор папки"
    $installDir = Select-InstallFolder $defaultDir
    Write-Host "Папка: $installDir"

    Write-Step "Сборка приложения"
    if (-not (Test-Path (Join-Path $PublishDir $AppExe))) {
        Write-Host "Publish ещё нет — собираю..."
        dotnet publish $Project -c Release -r win-x64 --self-contained false
    }
    else {
        Write-Host "Использую уже собранный publish."
    }

    if (-not (Test-Path (Join-Path $PublishDir $AppExe))) {
        throw "Не найден $AppExe после publish."
    }

    Write-Step "Установка"
    Stop-AppIfRunning
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    Copy-Item -Path (Join-Path $PublishDir "*") -Destination $installDir -Recurse -Force

    $exePath = Join-Path $installDir $AppExe
    if (-not (Test-Path $exePath)) {
        throw "Файл не скопировался: $exePath"
    }

    Write-Step "Ярлыки и автозапуск"
    $shell = New-Object -ComObject WScript.Shell
    $startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName"
    New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu "$AppName.lnk"))
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Save()

    New-Item -Path $RegPath -Force | Out-Null
    Set-ItemProperty -Path $RegPath -Name InstallPath -Value $installDir
    Set-ItemProperty -Path $RegPath -Name DisplayVersion -Value $AppVersion

    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $runKey -Name $AppName -Value "`"$exePath`""

    Write-Step "Готово"
    Write-Host "Установлено в: $installDir"
    Write-Host "Версия: $AppVersion"

    $launch = Read-Host "Запустить сейчас? (Y/n)"
    if ($launch -notin @("n", "N")) {
        Start-Process $exePath
    }
}
catch {
    Write-Host ""
    Write-Host "Ошибка: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Write-Host ""
    Read-Host "Нажмите Enter, чтобы закрыть"
}
