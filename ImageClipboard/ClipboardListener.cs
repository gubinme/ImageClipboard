namespace ImageClipboard;

internal sealed class ClipboardListener : Form
{
    private readonly System.Windows.Forms.Timer _pollTimer;
    private bool _isUpdatingClipboard;
    private string? _lastClipboardSignature;
    private long _updateSequence;
    private long _lastShownUpdateSequence = -1;

    public event EventHandler<ClipboardImage>? ImageCopied;

    public bool IsActive { get; set; } = true;

    public ClipboardListener()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(0, 0);
        Opacity = 0;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);

        _pollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _pollTimer.Tick += (_, _) => CheckClipboard(fromClipboardUpdate: false);
    }

    public void StartListening()
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        base.SetVisibleCore(false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.AddClipboardFormatListener(Handle);
        _pollTimer.Start();
        InitializeClipboardBaseline();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        NativeMethods.RemoveClipboardFormatListener(Handle);
        _pollTimer.Stop();
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            _updateSequence++;
            BeginInvoke(() => CheckClipboard(fromClipboardUpdate: true));
        }

        base.WndProc(ref m);
    }

    public void CheckClipboard(bool fromClipboardUpdate)
    {
        if (!IsActive || _isUpdatingClipboard || IsDisposed)
        {
            return;
        }

        string? signature;
        try
        {
            signature = ClipboardImageHelper.GetClipboardSignature();
        }
        catch
        {
            return;
        }

        var isRepeatCopy = fromClipboardUpdate && _updateSequence != _lastShownUpdateSequence;
        var isNewContent = !string.IsNullOrEmpty(signature) && signature != _lastClipboardSignature;
        if (!isRepeatCopy && !isNewContent)
        {
            return;
        }

        ClipboardImage? clipboardImage;
        try
        {
            clipboardImage = ClipboardImageHelper.TryGetImage();
        }
        catch
        {
            return;
        }

        if (clipboardImage is null)
        {
            return;
        }

        signature ??= ClipboardImageHelper.GetImageSignature(clipboardImage.Image);
        _lastClipboardSignature = signature;
        _lastShownUpdateSequence = _updateSequence;
        ImageCopied?.Invoke(this, clipboardImage);
    }

    private void InitializeClipboardBaseline()
    {
        try
        {
            _lastClipboardSignature = ClipboardImageHelper.GetClipboardSignature();
            if (_lastClipboardSignature is null)
            {
                using var image = ClipboardImageHelper.TryGetImage();
                if (image is not null)
                {
                    _lastClipboardSignature = ClipboardImageHelper.GetImageSignature(image.Image);
                }
            }
        }
        catch
        {
            _lastClipboardSignature = null;
        }

        _lastShownUpdateSequence = _updateSequence;
    }

    public void IgnoreNextUpdate(Action action)
    {
        _isUpdatingClipboard = true;
        try
        {
            action();
        }
        finally
        {
            _isUpdatingClipboard = false;
        }
    }
}
