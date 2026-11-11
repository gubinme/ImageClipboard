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

        var selectedScreen = _settings.GetSelectedScreen();
        var index = 1;

        foreach (var screen in Screen.AllScreens)
        {
            var isPrimary = screen.Primary;
            var label = isPrimary
                ? $"Монитор {index} (основной)"
                : $"Монитор {index}";

            var item = new ToolStripMenuItem(label)
            {
                Checked = screen.DeviceName == selectedScreen.DeviceName,
                Tag = screen.DeviceName
            };

            item.Click += (_, _) =>
            {
                if (item.Tag is not string deviceName)
                {
                    return;
                }

                _settings.MonitorDeviceName = deviceName;
                _settings.Save();

                foreach (ToolStripMenuItem sibling in monitorMenu.DropDownItems)
                {
                    sibling.Checked = ReferenceEquals(sibling, item);
                }
            };

            monitorMenu.DropDownItems.Add(item);
            index++;
        }
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
        var preview = new ImagePreviewForm(clipboardImage, screen);
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

    private void ShowAboutBalloon()
    {
        _trayIcon.ShowBalloonTip(
            3000,
            "ImageClipboard",
            "Копируйте изображение — оно появится в углу экрана. Перетащите его куда нужно.",
            ToolTipIcon.Info);
    }
}
