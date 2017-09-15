using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Advanced_Combat_Tracker;

namespace ACT.A12Helper
{
    internal partial class Overlay : Window
    {
        private static object instanceSync = new object();
        private static Overlay instance;
        public static Overlay Instance
        {
            get
            {
                lock (instanceSync)
                    return instance;
            }
        }
        private static Overlay CreateOverlay(bool clickThrough)
        {
            lock (instanceSync)
            {
                if (instance != null)
                {
                    if (instance.m_clickThrough != clickThrough)
                    {
                        instance.Close();
                        instance = new Overlay(clickThrough);
                    }
                }
                else if (instance == null)
                    instance = new Overlay(clickThrough);
            }
            return instance;
        }

        public static void OverlayShow()
        {
            ActGlobals.oFormActMain.Invoke(new Action(OverlayShowPriv));
        }
        private static void OverlayShowPriv()
        {
            CreateOverlay(false).Show();
        }
        public static void OverlayHide()
        {
            ActGlobals.oFormActMain.Invoke(new Action(instance.Hide));
        }

        public static void OverlayShow(A12Position pos, A12Position pos2, string cond)
        {
            ActGlobals.oFormActMain.Invoke(new Action<A12Position, A12Position, string>(OverlayShowPriv), pos, pos2, cond);
        }
        private static void OverlayShowPriv(A12Position pos, A12Position pos2, string cond)
        {
            var win = CreateOverlay(true);
            win.SetRotate(pos, pos2, cond);
            win.Show();
            win.SetTimer(TimeSpan.FromSeconds(10));
        }

        private readonly DispatcherTimer m_timer;
        private readonly bool m_clickThrough;

        public Overlay(bool clickThrough)
        {
            this.m_clickThrough = clickThrough;

            InitializeComponent();
            this.DataContext = OverlayModel.Instance;

            var interop = new WindowInteropHelper(this);
            interop.EnsureHandle();

            var hwnd = interop.Handle;
            var extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            if (clickThrough)
                extendedStyle |= NativeMethods.WS_EX_TRANSPARENT;

            extendedStyle |= NativeMethods.WS_EX_NOACTIVATE;

            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle);

            this.m_timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, this.Dispatcher);
            this.m_timer.Tick += (ls, le) =>
            {
                try
                {
                    this.Hide();
                }
                catch
                { }
            };
        }

        private void SetTimer(TimeSpan interval)
        {
            if (interval == TimeSpan.Zero)
                this.m_timer.Stop();
            else
            {
                this.m_timer.Interval = interval;
                this.m_timer.Start();
            }
        }

        private new void Hide()
        {
            this.SetTimer(TimeSpan.Zero);
            base.Hide();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private A12Position m_pos;
        private A12Position m_pos2;
        private void SetRotate(A12Position pos, A12Position pos2, string cond)
        {
            this.m_pos = pos;
            
            instance.ArrawRotate.Angle = Pos2Angle(pos);

            if (pos2 == A12Position.None)
            {
                this.ArrowIf.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.ArrowIf.Visibility = Visibility.Visible;
                
                instance.ArrowRotateIf.Angle = Pos2Angle(pos2);
                instance.IfText.Text = cond;
            }
        }
        public static double Pos2Angle(A12Position pos)
        {
            switch (pos)
            {
                case A12Position.BottomLeft:  return 180 + 20;
                case A12Position.BottomRight: return 180 - 20;
                case A12Position.Left:        return 270;
                case A12Position.Right:       return 90;
                case A12Position.Top:         return 0;
            }
            return 0;
        }
        public void Rotate()
        {
            this.m_pos2 = this.m_pos = (A12Position)(((int)this.m_pos % 5) + 1);
            this.SetRotate(this.m_pos, this.m_pos2, "1 2 3");
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hwnd, int index);

            [DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

            public const int GWL_EXSTYLE = (-20);
            public const int WS_EX_NOACTIVATE = 0x08000000;
            public const int WS_EX_TRANSPARENT = 0x00000020;
        }
    }
}
