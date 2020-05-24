using System;
using System.IO.Ports;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public partial class SelectComForm : Form
    {
        public string SELECTED_COMM_PORT = "EXIT";

        public SelectComForm()
        {
            InitializeComponent();
        }

        private void SelectCom_Load(object sender, EventArgs e)
        {
            //cboPorts.Items.Clear();
            //string[] ports = SerialPort.GetPortNames().OrderBy(s => Convert.ToInt32(s.Substring(3))).ToArray();
            //foreach (string port in ports)
            //    cboPorts.Items.Add(port);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SELECTED_COMM_PORT = cboPorts.Text;
            this.Close();
        }

    }
}
