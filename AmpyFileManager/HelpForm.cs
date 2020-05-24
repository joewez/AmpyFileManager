using AmpyFileManager.Properties;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
        }

        private void HelpForm_Load(object sender, System.EventArgs e)
        {
            RestoreWindow();
        }

        private void HelpForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveWindow();
        }

        private void RestoreWindow()
        {
            Width = Settings.Default.HelpWidth;
            Height = Settings.Default.HelpHeight;
            Top = Settings.Default.HelpTop < 0 ? 0 : Settings.Default.HelpTop;
            Left = Settings.Default.HelpLeft < 0 ? 0 : Settings.Default.HelpLeft;
        }

        private void SaveWindow()
        {
            Settings.Default.HelpHeight = Height;
            Settings.Default.HelpWidth = Width;
            Settings.Default.HelpLeft = Left;
            Settings.Default.HelpTop = Top;
            Settings.Default.Save();
        }

    }
}
