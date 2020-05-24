using AmpyFileManager.Properties;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public partial class TerminalForm : Form
    {
        private string _command = string.Empty;
        private bool _command_run = false;
        private string _readBuffer = string.Empty;
        private int _bufferLimit = 16384;
        private int _bufferResetSize = 2048;

        public TerminalForm(string ComPort, int BaudRate, string Command)
        {
            InitializeComponent();
            serialPort1.PortName = ComPort;
            serialPort1.BaudRate = BaudRate;
            _command = Command;
        }

        private void TerminalForm_Load(object sender, EventArgs e)
        {
            txtDisplay.Font = new Font(ConfigurationManager.AppSettings["TerminalFont"], Convert.ToSingle(ConfigurationManager.AppSettings["TerminalFontSize"]), FontStyle.Bold);
            txtDisplay.BackColor = DecodeColor("TerminalBackColor");
            txtDisplay.ForeColor = DecodeColor("TerminalForeColor");

            GetWindowValue();
        }

        private void TerminalForm_Activated(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
                serialPort1.Open();
            if (!_command_run)
                timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            if (!String.IsNullOrEmpty(_command) && serialPort1.IsOpen)
            {
                serialPort1.Write(_command);
                serialPort1.Write("\r");
            }
            _command_run = true;
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                _readBuffer = serialPort1.ReadExisting();
                this.Invoke(new EventHandler(DoUpdate));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "serialPort1_DataReceived() Error");
            }
        }

        private void txtDisplay_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                e.IsInputKey = true;
        }

        private void txtDisplay_MouseMove(object sender, MouseEventArgs e)
        {
            txtDisplay.SelectionLength = 0;
        }

        private void txtDisplay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                byte[] b = { 27, 91, 65 };
                serialPort1.Write(b, 0, 3);
            }
            else if (e.KeyCode == Keys.Down)
            {
                byte[] b = { 27, 91, 66 };
                serialPort1.Write(b, 0, 3);
            }
            else if (e.KeyCode == Keys.Tab)
            {
                serialPort1.Write("\t");
            }
            else if ((e.KeyCode == Keys.V && e.Control) || (e.KeyCode == Keys.Insert && e.Shift))
            {
                serialPort1.Write(Clipboard.GetText());
            }
        }

        private void txtDisplay_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                char[] key = new char[1];
                key[0] = e.KeyChar;
                serialPort1.Write(key, 0, 1);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void TerminalForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            serialPort1.Close();
            SaveWindowValue();
        }

        public void DoUpdate(object sender, System.EventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty(_readBuffer))
                {
                    // process a single backspace
                    if (_readBuffer == "\b\u001b[K")
                    {
                        txtDisplay.SelectionStart = txtDisplay.Text.Length - 1;
                        txtDisplay.SelectionLength = 1;
                        txtDisplay.SelectedText = "";
                    }
                    else if (_readBuffer[0] == 27 && _readBuffer[1] == 91)  // else if it begins with an escape sequence...
                    {
                        string cmd = _readBuffer.Substring(2);
                        //MessageBox.Show(cmd);
                        int pos = cmd.IndexOf('D');
                        if (pos > 0)
                        {
                            string countstr = cmd.Substring(0, pos);
                            int count = Convert.ToInt16(countstr);
                            if (count > 0)
                            {
                                txtDisplay.SelectionStart = txtDisplay.Text.Length - count;
                                txtDisplay.SelectionLength = count;
                                txtDisplay.SelectedText = "";
                            }
                            string remainder = cmd.Substring(pos + 1);
                            if (remainder != "")
                            {
                                //MessageBox.Show(remainder);
                                if (remainder == "\b\u001b[K")
                                {
                                    txtDisplay.SelectionStart = txtDisplay.Text.Length - 1;
                                    txtDisplay.SelectionLength = 1;
                                    txtDisplay.SelectedText = "";
                                }
                                else
                                {
                                    if (remainder[0] == 27 && remainder[1] == 91 && remainder[2] == 75)
                                    {
                                        txtDisplay.SelectionStart = txtDisplay.Text.Length;
                                        txtDisplay.SelectionLength = 0;
                                        txtDisplay.SelectedText = remainder.Substring(3);
                                    }
                                    else
                                    {
                                        txtDisplay.AppendText(remainder);
                                        txtDisplay.SelectionStart = txtDisplay.Text.Length;
                                        txtDisplay.SelectionLength = 0;
                                        txtDisplay.ScrollToCaret();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (cmd == "K")
                            {
                                txtDisplay.SelectionStart = txtDisplay.Text.Length - 1;
                                txtDisplay.SelectionLength = 1;
                                txtDisplay.SelectedText = "";
                            }
                            else
                                MessageBox.Show(cmd);
                        }
                    }
                    else // else it is just some text from the device
                    {
                        txtDisplay.AppendText(_readBuffer);
                        txtDisplay.SelectionStart = txtDisplay.Text.Length;
                        txtDisplay.SelectionLength = 0;
                        txtDisplay.ScrollToCaret();
                    }

                    // truncate the terminal buffer
                    if (txtDisplay.TextLength > _bufferLimit)
                    {
                        txtDisplay.Text = txtDisplay.Text.Substring(txtDisplay.TextLength - _bufferResetSize);
                    }

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("DoUpdate() Error:" + ex.Message);
            }
        
        }

        private Color DecodeColor(string ColorSettingName)
        {
            Color color = new Color();

            string ColorSetting = ConfigurationManager.AppSettings[ColorSettingName];

            if (ColorSetting.Contains(","))
            {
                string[] rgb = ColorSetting.Split(',');
                color = Color.FromArgb(Convert.ToInt32(rgb[0]), Convert.ToInt32(rgb[1]), Convert.ToInt32(rgb[2]));
            }
            else
                color = Color.FromName(ColorSetting);

            return color;
        }

        private void GetWindowValue()
        {
            Width = Settings.Default.REPLWidth;
            Height = Settings.Default.REPLHeight;
            Top = Settings.Default.REPLTop < 0 ? 0 : Settings.Default.REPLTop;
            Left = Settings.Default.REPLLeft < 0 ? 0 : Settings.Default.REPLLeft;
        }

        private void SaveWindowValue()
        {
            Settings.Default.REPLHeight = Height;
            Settings.Default.REPLWidth = Width;
            Settings.Default.REPLLeft = Left;
            Settings.Default.REPLTop = Top;
            Settings.Default.Save();
        }

    }
}
