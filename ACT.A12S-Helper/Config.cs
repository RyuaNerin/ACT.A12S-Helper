using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ACT.A12Helper.Properties;

namespace ACT.A12Helper
{
    internal partial class Config : UserControl
    {
        private static Config instance;
        public  static Config Instance => (instance ?? (instance = new Config()));

        public static void WriteLog(string format, params object[] args)
        {
#if DEBUG
            WriteLog(string.Format(format, args));
#endif
        }
        public static void WriteLog(string str)
        {
#if DEBUG
            if (Instance.InvokeRequired)
                Instance.Invoke(new Action<string>(WriteLogPriv), str);
            else
                WriteLogPriv(str);
        }

        private static void WriteLogPriv(string str)
        {
            Instance.richTextBox1.AppendText(Environment.NewLine);
            Instance.richTextBox1.AppendText(DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss.fff] "));
            Instance.richTextBox1.AppendText(str);

            Instance.richTextBox1.SelectionStart = Instance.richTextBox1.Text.Length;
            Instance.richTextBox1.ScrollToCaret();
#endif
        }
        
        private TextBox[] TextBoxes = new TextBox[8];

        public Config()
        {
            InitializeComponent();

#if !DEBUG
            this.panel1.Visible = false;
#endif

            this.Dock = DockStyle.Fill;

            var allItems = this.groupBox1.Controls.OfType<Control>();
            foreach (var le in allItems.OfType<TextBox>())
            {
                var id = int.Parse(le.Tag.ToString());
                le.Text = Plugin.Instance.PlayerInfos[id].Name;
                le.TextChanged += this.PartyInfo_Changed;
                this.TextBoxes[id] = le;
            }
            foreach (var le in allItems.OfType<RadioButton>())
            {
                if (int.Parse(le.Tag.ToString()) == Plugin.Instance.MyIndex)
                {
                    le.Checked = true;

                    var id = int.Parse(le.Tag.ToString());
                    for (int i = 0; i < 8; ++i)
                        this.TextBoxes[i].Enabled = Plugin.Relationship[id, i];
                }
                le.CheckedChanged += this.PartyInfo_Changed;
            }
            foreach (var le in allItems.OfType<ComboBox>())
            {
                le.SelectedIndex = (int)Plugin.Instance.PlayerInfos[int.Parse(le.Tag.ToString())].FirstPos;
                le.SelectedIndexChanged += this.PartyInfo_Changed;
            }

            this.numericUpDown3.Value = (decimal)(Settings.Default.OverlayScale   * 100);
            this.numericUpDown5.Value = (decimal)(Settings.Default.OverlayOpacity * 100);

            this.label11.BackColor = Settings.Default.FillColor;
            this.label12.BackColor = Settings.Default.StrokeColor;
        }

        private void PartyInfo_Changed(object sender, EventArgs e)
        {
            this.button2.Enabled = true;

            if (sender is RadioButton radio)
            {
                if (radio.Checked)
                {
                    var id = int.Parse(radio.Tag.ToString());
                    for (int i = 0; i < 8; ++i)
                        this.TextBoxes[i].Enabled = Plugin.Relationship[id, i];
                }
            }
        }

        private void Config_Load(object sender, EventArgs e)
        {
            this.numericUpDown1.DataBindings.Add(new Binding("Value", OverlayModel.Instance, "Left", false, DataSourceUpdateMode.OnPropertyChanged));
            this.numericUpDown2.DataBindings.Add(new Binding("Value", OverlayModel.Instance, "Top",  false, DataSourceUpdateMode.OnPropertyChanged));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var allItems = this.groupBox1.Controls.OfType<Control>().ToArray();

            foreach (var le in allItems.OfType<TextBox>())
                Plugin.Instance.PlayerInfos[int.Parse(le.Tag.ToString())].Name = le.Text.Trim();

            foreach (var le in allItems.OfType<ComboBox>())
                Plugin.Instance.PlayerInfos[int.Parse(le.Tag.ToString())].FirstPos = (A12Position)le.SelectedIndex;

            Plugin.Instance.MyIndex = int.Parse(allItems.First(l => (l is RadioButton rb) && rb.Checked).Tag.ToString());

            Plugin.DebugPlayerInfo();

            this.button2.Enabled = false;

            Settings.Default.Save();
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            OverlayModel.Instance.Scale = (double)this.numericUpDown3.Value / 100;
        }

        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            OverlayModel.Instance.Opacity = (double)this.numericUpDown5.Value / 100;
        }

        private void label11_Click(object sender, EventArgs e)
        {
            if (this.colorDialog1.ShowDialog() == DialogResult.OK)
            {
                this.label11.BackColor = this.colorDialog1.Color;
                OverlayModel.Instance.FillColor = this.colorDialog1.Color;
            }
        }

        private void label12_Click(object sender, EventArgs e)
        {
            if (this.colorDialog1.ShowDialog() == DialogResult.OK)
            {
                this.label12.BackColor = this.colorDialog1.Color;
                OverlayModel.Instance.StrokeColor = this.colorDialog1.Color;
            }
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            OverlayModel.Instance.StrokeThickness = (double)this.numericUpDown4.Value;
        }

        private bool m_showOverlay = false;
        private void button1_Click(object sender, EventArgs e)
        {
            if (this.m_showOverlay)
            {
                Overlay.OverlayHide();
                this.button1.Text = "표시";
            }
            else
            {
                Overlay.OverlayShow();
                this.button1.Text = "숨기기";
            }

            this.m_showOverlay = !this.m_showOverlay;
            this.button3.Enabled = this.m_showOverlay;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Overlay.Instance.Rotate();
        }

        private readonly ManualResetEvent m_pause = new ManualResetEvent(true);
        private volatile bool m_simulatorWorking = false;
        private Task m_simulator;
        private void button4_Click(object sender, EventArgs e)
        {
            if (this.m_simulator?.Status == TaskStatus.Running)
            {
                this.m_simulatorWorking = false;
                this.button4.Enabled = false;

                this.progressBar1.Value = this.progressBar1.Maximum;
            }
            else
            {
                if (this.openFileDialog1.ShowDialog() != DialogResult.OK)
                    return;

                this.m_simulatorWorking = true;

                var path = this.openFileDialog1.FileName;
            
                this.m_simulator = Task.Run(() =>
                {
                    using (var reader = new StreamReader(path, Encoding.UTF8, true))
                    {
                        this.Invoke(new Action(() => this.progressBar1.Maximum = (int)reader.BaseStream.Length));

                        string line;

                        Plugin.Instance.Start();
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!this.m_simulatorWorking)
                                break;

                            this.Invoke(new Action(() =>
                            {
                                this.progressBar1.Value = (int)reader.BaseStream.Position;
                                this.label13.Text = string.Format("{0:##0.0%} ({1:#,##0} / {2:#,##0})",
                                    (double)reader.BaseStream.Position / reader.BaseStream.Length,
                                    reader.BaseStream.Position,
                                    reader.BaseStream.Length);
                            }));
                            this.m_pause.WaitOne();

                            Plugin.Instance.InputLog(line);

                            Thread.Sleep(3);
                        }

                        Plugin.Instance.Stop();
                    }

                    this.Invoke(new Action(() => this.button4.Enabled = true));
                });
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (this.m_pause.WaitOne(0))
            {
                this.m_pause.Reset();
                this.progressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                this.m_pause.Set();
                this.progressBar1.Style = ProgressBarStyle.Continuous;
            }            
        }

        private void Config_Leave(object sender, EventArgs e)
        {
            Settings.Default.Save();
        }
    }
}
