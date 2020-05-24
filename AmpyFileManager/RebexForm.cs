using AmpyFileManager.Properties;
using Rebex.TerminalEmulation;
using System;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public partial class RebexForm : Form
    {
        private SerialPortChannel _serialPort = null;
        private string _comPort = string.Empty;
        private int _baudRate = 115200;
        private string _command = string.Empty;
        private bool _initialized = false;

        public RebexForm(string ComPort, int BaudRate, string Command)
        {
            InitializeComponent();

            _comPort = ComPort;
            _baudRate = BaudRate;
            _command = Command;

            this.DialogResult = DialogResult.No;
        }

        private void REPLForm_Load(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private void REPLForm_Activated(object sender, EventArgs e)
        {
            if (!_initialized)
                timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            // setup font styling of terminal control
            terminalControl1.Font = new Font(ConfigurationManager.AppSettings["TerminalFont"], Convert.ToSingle(ConfigurationManager.AppSettings["TerminalFontSize"]), FontStyle.Bold);

            // create a new Serial port object
            _serialPort = new SerialPortChannel(_comPort, _baudRate);

            // bind the Serial port instance to the terminal console
            terminalControl1.Bind(_serialPort);

            // execute any pre-defined command
            Scripting scripting = terminalControl1.Scripting;
            scripting.Send("\r");
            if (!String.IsNullOrEmpty(_command))
                scripting.Send(_command + "\r");

            _initialized = true;
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Yes;
            this.Close();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RebexAboutForm aboutForm = new RebexAboutForm();
            aboutForm.ShowDialog();
        }

        private void REPLForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            terminalControl1.Unbind();
            SaveWindow();
        }

        private void RestoreWindow()
        {
            Width = Settings.Default.REPLWidth;
            Height = Settings.Default.REPLHeight;
            Top = Settings.Default.REPLTop < 0 ? 0 : Settings.Default.REPLTop;
            Left = Settings.Default.REPLLeft < 0 ? 0 : Settings.Default.REPLLeft;
        }

        private void SaveWindow()
        {
            Settings.Default.REPLHeight = Height;
            Settings.Default.REPLWidth = Width;
            Settings.Default.REPLLeft = Left;
            Settings.Default.REPLTop = Top;
            Settings.Default.Save();
        }

    }
}
