using System.Diagnostics;

namespace ImageClipboard;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ClipboardListener _clipboardListener;
    private readonly ToolStripMenuItem _enabledItem;
    private ImagePreviewForm? _previewForm;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        AutoStartManager.SyncWithSettings(_settings);

        _clipboardListener = new ClipboardListener
        {
            IsActive = _settings.IsEnabled
        };
        _clipboardListener.ImageCopied += OnImageCopied;
        _clipboardListener.StartListening();

        _enabledItem = new ToolStripMenuItem("Включен")
        {
            Checked = _settings.IsEnabled,
            CheckOnClick = true
        };
        _enabledItem.CheckedChanged += (_, _) => SetEnabled(_enabledItem.Checked);

        _trayIcon = new NotifyIcon
        {
            Icon = _settings.IsEnabled ? IconFactory.TrayIcon : IconFactory.TrayIconDisabled,
            Text = GetTrayText(),
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowAboutBalloon();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClosePreviewForm();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _clipboardListener.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());

        var monitorMenu = new ToolStripMenuItem("Монитор");
        RefreshMonitorMenu(monitorMenu);
        menu.Items.Add(monitorMenu);

        var cornerMenu = new ToolStripMenuItem("Угол");
        RefreshCornerMenu(cornerMenu);
        menu.Items.Add(cornerMenu);

        var autoStartItem = new ToolStripMenuItem("Запускать с Windows")
        {
            Checked = _settings.AutoStart,
            CheckOnClick = true
        };
        autoStartItem.CheckedChanged += (_, _) =>
        {
            _settings.AutoStart = autoStartItem.Checked;
            AutoStartManager.SetEnabled(_settings.AutoStart);
            _settings.Save();
        };
        menu.Items.Add(autoStartItem);

        var openFolderItem = new ToolStripMenuItem("Открыть папку программы");
        openFolderItem.Click += (_, _) => OpenInstallFolder();
        menu.Items.Add(openFolderItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void SetEnabled(bool enabled)
    {
        _settings.IsEnabled = enabled;
        _clipboardListener.IsActive = enabled;
        _settings.Save();

        _trayIcon.Icon = enabled ? IconFactory.TrayIcon : IconFactory.TrayIconDisabled;
        _trayIcon.Text = GetTrayText();

        if (!enabled)
        {
            ClosePreviewForm();
        }
    }

    private string GetTrayText()
    {
        return _settings.IsEnabled
            ? "ImageClipboard"
            : "ImageClipboard (выключен)";
    }

    private void RefreshMonitorMenu(ToolStripMenuItem monitorMenu)
    {
        monitorMenu.DropDownItems.Clear();

        var primaryItem = new ToolStripMenuItem("Основной монитор Windows")
        {
            Checked = _settings.UsePrimaryMonitor
        };
        primaryItem.Click += (_, _) =>
        {
            _settings.UsePrimaryMonitor = true;
            _settings.Save();
            RefreshMonitorMenu(monitorMenu);
        };
        monitorMenu.DropDownItems.Add(primaryItem);
        monitorMenu.DropDownItems.Add(new ToolStripSeparator());

        var index = 1;
        foreach (var screen in MonitorHelper.GetOrderedScreens())
        {
            var fingerprint = MonitorHelper.GetFingerprint(screen);
            var item = new ToolStripMenuItem(MonitorHelper.GetDisplayLabel(screen, index))
            {
                Checked = !_settings.UsePrimaryMonitor && _settings.MonitorFingerprint == fingerprint,
                Tag = fingerprint
            };

            item.Click += (_, _) =>
            {
                if (item.Tag is not string selectedFingerprint)
                {
                    return;
                }

                _settings.UsePrimaryMonitor = false;
                _settings.MonitorFingerprint = selectedFingerprint;
                _settings.Save();
                RefreshMonitorMenu(monitorMenu);
            };

            monitorMenu.DropDownItems.Add(item);
            index++;
        }
    }

    private void RefreshCornerMenu(ToolStripMenuItem cornerMenu)
    {
        cornerMenu.DropDownItems.Clear();

        AddCornerItem(cornerMenu, "Слева сверху", PreviewCorner.TopLeft);
        AddCornerItem(cornerMenu, "Справа сверху", PreviewCorner.TopRight);
        AddCornerItem(cornerMenu, "Слева снизу", PreviewCorner.BottomLeft);
        AddCornerItem(cornerMenu, "Справа снизу", PreviewCorner.BottomRight);
    }

    private void AddCornerItem(ToolStripMenuItem cornerMenu, string text, PreviewCorner corner)
    {
        var item = new ToolStripMenuItem(text)
        {
            Checked = _settings.PreviewCorner == corner,
            Tag = corner
        };
        item.Click += (_, _) =>
        {
            _settings.PreviewCorner = corner;
            _settings.Save();
            RefreshCornerMenu(cornerMenu);
        };
        cornerMenu.DropDownItems.Add(item);
    }

    private void OnImageCopied(object? sender, ClipboardImage clipboardImage)
    {
        if (!_settings.IsEnabled || clipboardImage is null)
        {
            clipboardImage?.Dispose();
            return;
        }

        if (_clipboardListener.InvokeRequired)
        {
            _clipboardListener.BeginInvoke(() => ShowPreview(clipboardImage));
            return;
        }

        ShowPreview(clipboardImage);
    }

    private void ShowPreview(ClipboardImage clipboardImage)
    {
        ClosePreviewForm();

        var screen = _settings.GetSelectedScreen();
        var preview = new ImagePreviewForm(clipboardImage, screen, _settings.PreviewCorner);
        preview.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_previewForm, preview))
            {
                _previewForm = null;
            }

            clipboardImage.Dispose();
        };

        _previewForm = preview;
        preview.Show();
    }

    private void ClosePreviewForm()
    {
        if (_previewForm is null || _previewForm.IsDisposed)
        {
            return;
        }

        var form = _previewForm;
        _previewForm = null;
        form.Close();
    }

    private static void OpenInstallFolder()
    {
        var folder = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true
        });
    }

    private void ShowAboutBalloon()
    {
        _trayIcon.ShowBalloonTip(
            3000,
            "ImageClipboard",
            "Копируйте изображение — оно появится в углу экрана. Перетащите его куда нужно.",
            ToolTipIcon.Info);
    }
}
