using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace POS.Cashier.UI.Services
{
    /// <summary>
    /// Applies one monitor-aware sizing and centering rule to Cashier windows.
    /// This keeps owned dialogs on the owner's monitor and prevents fixed-size
    /// windows from opening outside the usable Windows work area.
    /// </summary>
    internal static class CashierWindowPlacementService
    {
        private const uint MonitorDefaultToNearest = 0x00000002;
        private const double WorkAreaMargin = 12d;
        private static int _isRegistered;

        public static void Register()
        {
            if (Interlocked.Exchange(ref _isRegistered, 1) != 0)
                return;

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window ||
                window.GetType().Assembly != typeof(CashierWindowPlacementService).Assembly ||
                window.WindowState != WindowState.Normal)
            {
                return;
            }

            window.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => FitAndCenter(window)));
        }

        private static void FitAndCenter(Window window)
        {
            if (!window.IsVisible || window.WindowState != WindowState.Normal)
                return;

            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;

            IntPtr monitorReferenceHandle =
                window.Owner is { IsVisible: true } ownerWindow
                    ? new WindowInteropHelper(ownerWindow).Handle
                    : handle;

            if (monitorReferenceHandle == IntPtr.Zero)
                monitorReferenceHandle = handle;

            Rect workArea = GetWorkArea(monitorReferenceHandle);
            if (workArea.IsEmpty || workArea.Width <= 0d || workArea.Height <= 0d)
                return;

            double availableWidth = Math.Max(320d, workArea.Width - (WorkAreaMargin * 2d));
            double availableHeight = Math.Max(240d, workArea.Height - (WorkAreaMargin * 2d));

            if (window.MinWidth > availableWidth)
                window.MinWidth = availableWidth;
            if (window.MinHeight > availableHeight)
                window.MinHeight = availableHeight;

            window.MaxWidth = Math.Min(window.MaxWidth, availableWidth);
            window.MaxHeight = Math.Min(window.MaxHeight, availableHeight);

            double width = ResolveWindowLength(window.Width, window.ActualWidth, availableWidth);
            double height = ResolveWindowLength(window.Height, window.ActualHeight, availableHeight);

            if (width > availableWidth)
            {
                window.Width = availableWidth;
                width = availableWidth;
            }

            if (height > availableHeight)
            {
                window.Height = availableHeight;
                height = availableHeight;
            }

            double targetLeft;
            double targetTop;

            if (window.Owner is { IsVisible: true } owner)
            {
                double ownerWidth = owner.ActualWidth > 0d ? owner.ActualWidth : owner.Width;
                double ownerHeight = owner.ActualHeight > 0d ? owner.ActualHeight : owner.Height;
                targetLeft = owner.Left + ((ownerWidth - width) / 2d);
                targetTop = owner.Top + ((ownerHeight - height) / 2d);
            }
            else
            {
                targetLeft = workArea.Left + ((workArea.Width - width) / 2d);
                targetTop = workArea.Top + ((workArea.Height - height) / 2d);
            }

            double maximumLeft = workArea.Right - WorkAreaMargin - width;
            double maximumTop = workArea.Bottom - WorkAreaMargin - height;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = Clamp(targetLeft, workArea.Left + WorkAreaMargin, maximumLeft);
            window.Top = Clamp(targetTop, workArea.Top + WorkAreaMargin, maximumTop);
        }

        private static double ResolveWindowLength(
            double configuredLength,
            double actualLength,
            double fallback)
        {
            if (!double.IsNaN(configuredLength) &&
                !double.IsInfinity(configuredLength) &&
                configuredLength > 0d)
            {
                return configuredLength;
            }

            if (!double.IsNaN(actualLength) &&
                !double.IsInfinity(actualLength) &&
                actualLength > 0d)
            {
                return actualLength;
            }

            return fallback;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                return minimum;

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static Rect GetWorkArea(IntPtr windowHandle)
        {
            IntPtr monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
                return SystemParameters.WorkArea;

            var monitorInfo = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>()
            };

            if (!GetMonitorInfo(monitor, ref monitorInfo))
                return SystemParameters.WorkArea;

            var source = HwndSource.FromHwnd(windowHandle);
            Matrix transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

            Point topLeft = transform.Transform(
                new Point(monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top));
            Point bottomRight = transform.Transform(
                new Point(monitorInfo.WorkArea.Right, monitorInfo.WorkArea.Bottom));

            return new Rect(topLeft, bottomRight);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public uint Flags;
        }
    }
}
