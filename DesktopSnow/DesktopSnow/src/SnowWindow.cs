using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace DesktopSnow
{
    internal sealed class SnowWindow : Window
    {
        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExNoActivate = 0x08000000L;
        private const int WmMouseActivate = 0x0021;
        private const int WmNcHitTest = 0x0084;
        private const int WmDpiChanged = 0x02E0;
        private const int MaNoActivate = 3;
        private const int HtTransparent = -1;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint NoSizeMoveFlags = SwpNoSize | SwpNoMove;

        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndBottom = new IntPtr(1);

        private readonly Forms.Screen _screen;
        private readonly SnowSurface _surface;
        private bool _alwaysOnTop;
        private IntPtr _handle;

        public SnowWindow(Forms.Screen screen, bool alwaysOnTop, int randomSeed)
        {
            _screen = screen;
            _alwaysOnTop = alwaysOnTop;

            double floorRatio = CalculateFloorRatio(screen);
            _surface = new SnowSurface(floorRatio, randomSeed);

            Title = "Desktop Snow";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            Topmost = alwaysOnTop;
            Left = 0.0;
            Top = 0.0;
            Width = 100.0;
            Height = 100.0;
            Content = _surface;

            SourceInitialized += HandleSourceInitialized;
            Loaded += HandleLoaded;
        }

        public string MonitorName
        {
            get { return _screen.DeviceName; }
        }

        public void UpdateSnow(double deltaSeconds, SnowIntensity intensity, bool paused)
        {
            _surface.UpdateSnow(deltaSeconds, intensity, paused);
        }

        public void ClearPile()
        {
            _surface.ClearPile();
        }

        public void StartSnowPlow()
        {
            _surface.StartSnowPlow();
        }

        public void SetAlwaysOnTop(bool alwaysOnTop)
        {
            _alwaysOnTop = alwaysOnTop;
            Topmost = alwaysOnTop;
            ApplyMonitorBounds();
        }

        public void PushToBottom()
        {
            if (_handle != IntPtr.Zero && !_alwaysOnTop)
            {
                SetWindowPos(
                    _handle,
                    HwndBottom,
                    0,
                    0,
                    0,
                    0,
                    SwpNoActivate | NoSizeMoveFlags);
            }
        }

        private void HandleSourceInitialized(object sender, EventArgs eventArgs)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            _handle = helper.Handle;

            long style = GetWindowLongPtr(_handle, GwlExStyle).ToInt64();
            style |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            SetWindowLongPtr(_handle, GwlExStyle, new IntPtr(style));

            HwndSource source = HwndSource.FromHwnd(_handle);
            if (source != null)
            {
                source.AddHook(WindowProcedure);
            }

            ApplyMonitorBounds();
        }

        private void HandleLoaded(object sender, RoutedEventArgs eventArgs)
        {
            ApplyMonitorBounds();
        }

        private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmNcHitTest)
            {
                handled = true;
                return new IntPtr(HtTransparent);
            }

            if (message == WmMouseActivate)
            {
                handled = true;
                return new IntPtr(MaNoActivate);
            }

            if (message == WmDpiChanged)
            {
                Dispatcher.BeginInvoke(new Action(ApplyMonitorBounds));
            }

            return IntPtr.Zero;
        }

        private void ApplyMonitorBounds()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            System.Drawing.Rectangle bounds = _screen.Bounds;
            SetWindowPos(
                _handle,
                _alwaysOnTop ? HwndTopmost : HwndBottom,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                SwpNoActivate | SwpShowWindow);
        }

        private static double CalculateFloorRatio(Forms.Screen screen)
        {
            System.Drawing.Rectangle bounds = screen.Bounds;
            System.Drawing.Rectangle work = screen.WorkingArea;

            int bottomGap = bounds.Bottom - work.Bottom;
            if (bottomGap > 1)
            {
                return Math.Max(0.05, Math.Min(1.0, (double)(work.Bottom - bounds.Top) / bounds.Height));
            }

            return 1.0;
        }

        private static IntPtr GetWindowLongPtr(IntPtr window, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(window, index)
                : new IntPtr(GetWindowLong32(window, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(window, index, newValue)
                : new IntPtr(SetWindowLong32(window, index, newValue.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr window, int index, int newValue);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr newValue);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
