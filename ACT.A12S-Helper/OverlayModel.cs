using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ACT.A12Helper.Properties;
using Color = System.Drawing.Color;

namespace ACT.A12Helper
{
    [Export(typeof(OverlayModel))]
    internal class OverlayModel : INotifyPropertyChanged
    {
        private static OverlayModel instance;
        public static OverlayModel Instance => instance ?? (instance = new OverlayModel());

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public OverlayModel()
        {
            instance = this;

            this.m_fillBrush = new SolidColorBrush(ToWPFColor(Settings.Default.FillColor));
            this.m_fillBrush.Freeze();

            this.m_strokeBrush = new SolidColorBrush(ToWPFColor(Settings.Default.StrokeColor));
            this.m_strokeBrush.Freeze();

            this.OnPropertyChanged("Top");
            this.OnPropertyChanged("Left");
            this.OnPropertyChanged("Scale");
            this.OnPropertyChanged("Opacity");

            this.OnPropertyChanged("FillBrush");
            this.OnPropertyChanged("StrokeBrush");
            this.OnPropertyChanged("StrokeThickness");
        }

        public double Top
        {
            get => Settings.Default.OverlayTop;
            set
            {
                Settings.Default.OverlayTop = value;
                this.OnPropertyChanged();
            }
        }
        public double Left
        {
            get => Settings.Default.OverlayLeft;
            set
            {
                Settings.Default.OverlayLeft = value;
                this.OnPropertyChanged();
            }
        }
        public double Scale
        {
            get => Settings.Default.OverlayScale;
            set
            {
                Settings.Default.OverlayScale = value;
                this.OnPropertyChanged();
            }
        }
        public double Opacity
        {
            get => Settings.Default.OverlayOpacity;
            set
            {
                Settings.Default.OverlayOpacity = value;
                this.OnPropertyChanged();
            }
        }
        public Color FillColor
        {
            get => Settings.Default.FillColor;
            set
            {
                Settings.Default.FillColor = value;
                this.m_fillBrush = new SolidColorBrush(ToWPFColor(value));
                this.m_fillBrush.Freeze();
                this.OnPropertyChanged("FillBrush");
            }
        }
        public Color StrokeColor
        {
            get => Settings.Default.StrokeColor;
            set
            {
                Settings.Default.StrokeColor = value;
                this.m_strokeBrush = new SolidColorBrush(ToWPFColor(value));
                this.m_strokeBrush.Freeze();
                this.OnPropertyChanged("StrokeBrush");
            }
        }
        public double StrokeThickness
        {
            get => Settings.Default.StrokeThickness;
            set
            {
                Settings.Default.StrokeThickness = value;
                this.OnPropertyChanged();
            }
        }

        private SolidColorBrush m_fillBrush;
        public SolidColorBrush FillBrush => this.m_fillBrush;

        private SolidColorBrush m_strokeBrush;
        public SolidColorBrush StrokeBrush => this.m_strokeBrush;

        public static System.Windows.Media.Color ToWPFColor(System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }
    }
}
