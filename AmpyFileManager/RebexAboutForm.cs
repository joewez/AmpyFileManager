using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public partial class RebexAboutForm : Form
    {
        public RebexAboutForm()
        {
            InitializeComponent();
        }

        private void lnkRebexTerminalComponent_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            HelpForm help = new HelpForm();
            help.Text = "Rebex Terminal Emulation Library";
            ((WebBrowser)help.Controls["webBrowser1"]).Url = new Uri("https://www.rebex.net/terminal-emulation.net/");
            help.Show();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
