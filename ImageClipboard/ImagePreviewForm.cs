using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ImageClipboard;

internal sealed class ImagePreviewForm : Form
{
    private const int TotalDurationMs = 5000;
    private const int FadeStartMs = 3000;
    private const int TimerIntervalMs = 40;
    private const int ScreenMargin = 16;
    private const int MaxPreviewWidth = 320;
    private const int MaxPreviewHeight = 240;
    private const int CornerRadius = 10;
    private const int CloseButtonSize = 24;
    private const int CloseButtonMargin = 6;

    private readonly System.Windows.Forms.Timer _lifecycleTimer;
    private readonly PreviewImagePanel _imagePanel;
    private readonly List<string> _tempFiles = [];
    private readonly ClipboardImage _clipboardImage;
    private readonly Image _image;
    private readonly PreviewCorner _corner;
    private readonly EventHandler _onFrameChangedHandler;
    private string? _materializedPath;
    private Point _mouseDownPoint;
    private bool _dragStarted;
    private int _elapsedMs;
    private bool _isPaused;
    private bool _closeButtonHovered;

    public ImagePreviewForm(ClipboardImage clipboardImage, Screen screen, PreviewCorner corner)
    {
        _clipboardImage = clipboardImage;
        _corner = corner;
        _image = clipboardImage.Image ?? throw new ArgumentException("Clipboard image is empty.", nameof(clipboardImage));
        _onFrameChangedHandler = OnFrameChanged;

        _lifecycleTimer = new System.Windows.Forms.Timer { Interval = TimerIntervalMs };
        _lifecycleTimer.Tick += OnLifecycleTick;

        _imagePanel = new PreviewImagePanel(_image)
        {
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand
        };
        _imagePanel.PaintOverlay += DrawCloseButton;
        _imagePanel.MouseDown += ImagePanelOnMouseDown;
        _imagePanel.MouseMove += ImagePanelOnMouseMove;
        _imagePanel.MouseUp += ImagePanelOnMouseUp;
        _imagePanel.MouseClick += ImagePanelOnMouseClick;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Opacity = 1;

        Controls.Add(_imagePanel);

        HookPauseEvents(this);
        HookPauseEvents(_imagePanel);

        SizeToImage(screen);
        PositionOnScreen(screen);
        ApplyRoundedShape();
        ResetLifecycle();
    }

    private Rectangle CloseButtonBounds =>
        new(
            ClientSize.Width - CloseButtonSize - CloseButtonMargin,
            CloseButtonMargin,
            CloseButtonSize,
            CloseButtonSize);

    private void OnFrameChanged(object? sender, EventArgs e)
    {
        _imagePanel.Invalidate();
    }

    private void ImagePanelOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || CloseButtonBounds.Contains(e.Location))
        {
            return;
        }

        _mouseDownPoint = e.Location;
        _dragStarted = false;
    }

    private void ImagePanelOnMouseMove(object? sender, MouseEventArgs e)
    {
        var hovered = CloseButtonBounds.Contains(e.Location);
        if (hovered != _closeButtonHovered)
        {
            _closeButtonHovered = hovered;
            _imagePanel.Invalidate(CloseButtonBounds);
        }

        if (e.Button != MouseButtons.Left || _dragStarted || CloseButtonBounds.Contains(_mouseDownPoint))
        {
            return;
        }

        var dragSize = SystemInformation.DragSize;
        if (Math.Abs(e.X - _mouseDownPoint.X) < dragSize.Width &&
            Math.Abs(e.Y - _mouseDownPoint.Y) < dragSize.Height)
        {
            return;
        }

        _dragStarted = true;
        StartDrag();
    }

    private void ImagePanelOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _dragStarted || CloseButtonBounds.Contains(e.Location))
        {
            return;
        }

        OpenInDefaultViewer();
    }

    private void ImagePanelOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && CloseButtonBounds.Contains(e.Location))
        {
            ClosePreview();
        }
    }

    private void StartDrag()
    {
        PauseLifecycle();

        var dataObject = CreateDragDataObject();
        try
        {
            DoDragDrop(dataObject, DragDropEffects.Copy);
        }
        finally
        {
            ResetLifecycle();
        }
    }

    private void OpenInDefaultViewer()
    {
        try
        {
            ImageFileHelper.OpenWithDefaultViewer(GetMaterializedPath());
        }
        catch
        {
            // Ignore viewer launch failures.
        }

        // Opening the viewer steals focus; keep the preview paused while the
        // cursor is still over it so the close animation can be held as usual.
        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            if (IsCursorInsideForm())
            {
                PauseLifecycle();
            }
        });
    }

    private string GetMaterializedPath()
    {
        return _materializedPath ??= ImageFileHelper.GetOrCreateFilePath(_clipboardImage, _tempFiles);
    }

    private void DrawCloseButton(Graphics graphics)
    {
        var bounds = CloseButtonBounds;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (_closeButtonHovered)
        {
            using var hoverBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            graphics.FillEllipse(hoverBrush, bounds);
        }

        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        const float arm = 4.5f;

        using var crossPen = new Pen(Color.FromArgb(235, 255, 255, 255), 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawLine(crossPen, centerX - arm, centerY - arm, centerX + arm, centerY + arm);
        graphics.DrawLine(crossPen, centerX + arm, centerY - arm, centerX - arm, centerY + arm);
    }

    private bool IsCursorInsideForm()
    {
        var clientPoint = PointToClient(Cursor.Position);
        return ClientRectangle.Contains(clientPoint);
    }

    private void HandlePointerEnter()
    {
        PauseLifecycle();
    }

    private void HandlePointerLeave()
    {
        BeginInvoke(() =>
        {
            if (!IsCursorInsideForm())
            {
                ResetLifecycle();
            }
        });
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (ImageAnimator.CanAnimate(_image))
        {
            ImageAnimator.Animate(_image, _onFrameChangedHandler);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _lifecycleTimer.Stop();
        _lifecycleTimer.Dispose();

        if (ImageAnimator.CanAnimate(_image))
        {
            ImageAnimator.StopAnimate(_image, _onFrameChangedHandler);
        }

        _imagePanel.PreviewImage = null;

        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                var directory = Path.GetDirectoryName(path);
                if (directory is not null && Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Temp files may still be in use by the drop target or viewer.
            }
        }

        base.OnFormClosed(e);
    }

    private void HookPauseEvents(Control control)
    {
        control.MouseEnter += (_, _) => HandlePointerEnter();
        control.MouseLeave += (_, _) => HandlePointerLeave();
    }

    private void PauseLifecycle()
    {
        _isPaused = true;
        Opacity = 1;
    }

    private void ResetLifecycle()
    {
        _isPaused = false;
        _elapsedMs = 0;
        Opacity = 1;
        _lifecycleTimer.Stop();
        _lifecycleTimer.Start();
    }

    private void OnLifecycleTick(object? sender, EventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        _elapsedMs += TimerIntervalMs;

        if (_elapsedMs >= TotalDurationMs)
        {
            ClosePreview();
            return;
        }

        if (_elapsedMs >= FadeStartMs)
        {
            var fadeProgress = (_elapsedMs - FadeStartMs) / (double)(TotalDurationMs - FadeStartMs);
            Opacity = Math.Max(0, 1.0 - fadeProgress);
        }
        else
        {
            Opacity = 1;
        }
    }

    private void SizeToImage(Screen screen)
    {
        var workingArea = screen.WorkingArea;
        var maxWidth = Math.Min(MaxPreviewWidth, workingArea.Width - ScreenMargin * 2);
        var maxHeight = Math.Min(MaxPreviewHeight, workingArea.Height - ScreenMargin * 2);

        var imageWidth = Math.Max(1, _image.Width);
        var imageHeight = Math.Max(1, _image.Height);

        var ratio = Math.Min(
            (double)maxWidth / imageWidth,
            (double)maxHeight / imageHeight);

        if (ratio > 1.0)
        {
            ratio = 1.0;
        }

        ClientSize = new Size(
            Math.Max(64, (int)Math.Round(imageWidth * ratio)),
            Math.Max(64, (int)Math.Round(imageHeight * ratio)));
    }

    private void PositionOnScreen(Screen screen)
    {
        var workingArea = screen.WorkingArea;
        var x = _corner is PreviewCorner.TopLeft or PreviewCorner.BottomLeft
            ? workingArea.Left + ScreenMargin
            : workingArea.Right - Width - ScreenMargin;
        var y = _corner is PreviewCorner.TopLeft or PreviewCorner.TopRight
            ? workingArea.Top + ScreenMargin
            : workingArea.Bottom - Height - ScreenMargin;

        Location = new Point(x, y);
    }

    private void ApplyRoundedShape()
    {
        using var path = CreateRoundedRectPath(ClientRectangle, CornerRadius);
        Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ClosePreview()
    {
        _lifecycleTimer.Stop();
        Close();
    }

    private DataObject CreateDragDataObject()
    {
        var path = GetMaterializedPath();
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { path });

        if (_clipboardImage.OriginalBytes is { Length: > 0 })
        {
            data.SetData(ImageDecoder.GetMimeType(_clipboardImage.DragExtension), false, _clipboardImage.OriginalBytes);
        }

        if (!ImageAnimator.CanAnimate(_image))
        {
            data.SetData(DataFormats.Bitmap, true, new Bitmap(_image));
        }

        return data;
    }

    private sealed class PreviewImagePanel : Panel
    {
        public PreviewImagePanel(Image previewImage)
        {
            PreviewImage = previewImage;
            DoubleBuffered = true;
            BackColor = Color.Black;
        }

        public Image? PreviewImage { get; set; }

        public event Action<Graphics>? PaintOverlay;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (PreviewImage is null)
            {
                return;
            }

            if (ImageAnimator.CanAnimate(PreviewImage))
            {
                ImageAnimator.UpdateFrames(PreviewImage);
            }

            var graphics = e.Graphics;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var destination = GetImageBounds(PreviewImage, ClientSize);
            graphics.DrawImage(PreviewImage, destination);

            PaintOverlay?.Invoke(graphics);
        }

        private static Rectangle GetImageBounds(Image image, Size clientSize)
        {
            var ratio = Math.Min(
                (double)clientSize.Width / image.Width,
                (double)clientSize.Height / image.Height);

            var width = Math.Max(1, (int)Math.Round(image.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(image.Height * ratio));
            var x = (clientSize.Width - width) / 2;
            var y = (clientSize.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }
    }
}
