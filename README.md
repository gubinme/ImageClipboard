<img width="3000" height="1000" alt="banner_imageclipboard" src="https://github.com/user-attachments/assets/50fdda7d-1d3d-4777-b58c-9af16d4e1f8e" />
A lightweight Windows tray app that shows a temporary preview whenever you copy an image — then lets you drag it into another app or onto the desktop.

## Features

- Watches the clipboard for image copies (browser, Explorer, screenshots, etc.)
- Shows a floating preview in the bottom-right corner of the selected monitor
- Auto-closes after 5 seconds (fades out after 3 seconds); hovering pauses the timer
- Drag & drop from the preview window (desktop, browsers, messengers, editors)
- Click the preview to open the image in your default Windows viewer (hover no longer pauses after that)
- Tray menu: enable/disable, monitor selection, preview corner, open install folder, start with Windows, exit
- Supports PNG, JPEG, GIF (animated preview), WebP, BMP, TIFF, and more
- No taskbar button for the tray app or the preview window

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Install

Download the latest installer from [Releases](https://github.com/gubinme/ImageClipboard/releases):

`ImageClipboardSetup-*.exe`

The installer lets you choose the install folder and can update an existing installation.

## Usage

1. Start ImageClipboard — a tray icon appears near the clock.
2. Copy any image (`Ctrl+C`, or browser context menu → Copy image).
3. A preview appears for a few seconds.
4. Drag it where you need it, click to open in the viewer, or wait for it to fade out.
5. Right-click the tray icon for settings.

## Build from source

```powershell
dotnet publish ImageClipboard\ImageClipboard.csproj -c Release -r win-x64 --self-contained false
```

Output:

`ImageClipboard\bin\Release\net8.0-windows\win-x64\publish\`

### Installer (optional)

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
.\installer\build-installer.ps1
```

Or install directly without building a setup EXE:

```powershell
.\installer\Install-ImageClipboard.ps1
```

## Settings

Stored in:

`%LocalAppData%\ImageClipboard\settings.json`

Monitor selection defaults to the Windows primary display. You can pin a specific monitor from the tray menu.

## License

Use and modify freely for personal or commercial projects.
