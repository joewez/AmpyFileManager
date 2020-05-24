using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScintillaNET;

namespace AmpyFileManager
{
    public partial class Manager : Form
    {
        private const string NEW_FILENAME = "<new>";
        private const string LF = "\n";
        private const string CR = "\r";
        private const string CRLF = "\r\n";
        private const string LBracket = "[";
        private const string RBracket = "]";

        private bool _FileDirty = false;
        private string _BackupPath = string.Empty;
        private string _SessionPath = string.Empty;
        private string _CurrentPath = string.Empty;
        private string _CurrentFile = string.Empty;
        private string _EditableExtensions = string.Empty;
        private string _command = string.Empty;
        private string _readBuffer = string.Empty;
        private bool _JustOpened = false;
        private string _runCommand = string.Empty;
        private int _bufferLimit = 16384;
        private int _bufferResetSize = 2048;

        private bool _externalTerminal = true;
        private string _terminalApp = "putty";
        private string _terminalAppArgs = "-load \"repl\" -serial {PORT}";

        private ESPRoutines _ESP;   // Wrapper for AMPY invocations

        public Manager(ESPRoutines ESP)
        {
            _ESP = ESP;
            InitializeComponent();            
        }

        #region Events

        private void Manager_Load(object sender, EventArgs e)
        {
            this.Text = "Ampy File Manager (" + _ESP.COMM_PORT + ")";

            _externalTerminal = (ConfigurationManager.AppSettings["REPL"] == "E");
            if (_externalTerminal)
            {
                _terminalApp = ConfigurationManager.AppSettings["TerminalApp"];
                _terminalAppArgs = ConfigurationManager.AppSettings["TerminalAppArgs"];
                splitContainer2.Panel2.Visible = false;
                splitContainer2.SplitterDistance = splitContainer2.Height;
            }

            // Get the dir where we save things
            string saveDir = ConfigurationManager.AppSettings["SaveDir"];
            if (String.IsNullOrWhiteSpace(saveDir))
                saveDir = Path.GetDirectoryName(Application.ExecutablePath);

            btnRun.Visible = (ConfigurationManager.AppSettings["ShowRunButton"] == "Y");
            btnBackup.Visible = (ConfigurationManager.AppSettings["ShowBackupButton"] == "Y");

            // directory for all backup files
            _BackupPath = Path.Combine(saveDir, "backups");
            if (!Directory.Exists(_BackupPath))
                Directory.CreateDirectory(_BackupPath);

            // Where we store our files while they are being edited
            string uniqueSessions = ConfigurationManager.AppSettings["UniqueSessions"];
            string SessionRoot = Path.Combine(saveDir, "session");
            if (uniqueSessions.Trim().ToUpper().StartsWith("Y"))
                _SessionPath = Path.Combine(SessionRoot, DateTime.Now.ToString("SyyyyMMdd-HHmm"));
            else
                _SessionPath = SessionRoot;
            if (!Directory.Exists(_SessionPath))
                Directory.CreateDirectory(_SessionPath);

            // load help links
            LoadHelpDropdown();

            // Setup tooltips
            toolTip1.SetToolTip(btnBackup, "Make a local backup of all the files on the device");
            toolTip1.SetToolTip(btnChangeMode, "Go to the MicroPython REPL");
            toolTip1.SetToolTip(btnDelete, "Delete the selected file or directory permanently from the device");
            toolTip1.SetToolTip(btnExport, "Export the selected file from the device to your computer");
            toolTip1.SetToolTip(btnLoad, "Import an external file to the device");
            toolTip1.SetToolTip(btnMkdir, "Make a sub-directory under the current directory");
            toolTip1.SetToolTip(btnMove, "Move (rename) the selected file");
            toolTip1.SetToolTip(btnNew, "Create a new file");
            toolTip1.SetToolTip(btnOpen, "Open the selected file for editing or directory for viewing");
            toolTip1.SetToolTip(btnRefresh, "Re-read the file list for the current directory");
            toolTip1.SetToolTip(btnRun, "Run the currently selected file");
            toolTip1.SetToolTip(btnSave, "Save the current file");
            toolTip1.SetToolTip(btnSaveAs, "Save the current file to the current directory with the specified name");
            toolTip1.SetToolTip(btnReplaceAll, "Simple search-and-replace for current file");
            toolTip1.SetToolTip(cboHelp, "Help Links");

            // these are the file that can be opened and edited
            _EditableExtensions = ConfigurationManager.AppSettings["EditExtensions"];
            if (String.IsNullOrWhiteSpace(_EditableExtensions))
                _EditableExtensions = "py,txt,html,js,css,json";

            // set some colors
            string bcolor = ConfigurationManager.AppSettings["ExplorerColor"];
            if (bcolor.Contains(","))
            {
                string[] rgb = bcolor.Split(',');
                lstDirectory.BackColor = Color.FromArgb(Convert.ToInt32(rgb[0]), Convert.ToInt32(rgb[1]), Convert.ToInt32(rgb[2]));
            }
            else
                lstDirectory.BackColor = Color.FromName(bcolor);

            txtTerminal.Font = new Font(ConfigurationManager.AppSettings["TerminalFont"], Convert.ToSingle(ConfigurationManager.AppSettings["TerminalFontSize"]), FontStyle.Regular);
            lstDirectory.Font = new Font(ConfigurationManager.AppSettings["DirectoryFont"], Convert.ToSingle(ConfigurationManager.AppSettings["DirectoryFontSize"]), FontStyle.Regular);

            ConfigureForPython(scintilla1);

            RefreshFileList();

            ResetNew();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (OKToContinue())
                OpenItem();
        }

        private void lstDirectory_DoubleClick(object sender, EventArgs e)
        {
            if (OKToContinue())
                OpenItem();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            ButtonSave();
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            if (_CurrentFile == NEW_FILENAME)
                ButtonSave();
            else
            {
                string oldFilename = _CurrentFile;
                _CurrentFile = NEW_FILENAME;
                bool saved = DoSave(oldFilename);
                if (saved)
                {
                    lblCurrentFile.Text = GetFileOnly(_CurrentFile);
                    _FileDirty = false;
                    RefreshFileList();
                }
                else
                    _CurrentFile = oldFilename;
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            if (OKToContinue())
                ResetNew();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (selectedItem.StartsWith(LBracket))
                {
                    string DirToDelete = selectedItem.Replace(LBracket, "").Replace(RBracket, "");
                    if (DirToDelete != "..")
                    {
                        string FullDirToDelete = (_CurrentPath == "") ? DirToDelete : _CurrentPath + "/" + DirToDelete;
                        if (MessageBox.Show("Are you sure you want to delete the directory '" + FullDirToDelete + "' and all of it's contents?", "Confirm Delete", MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
                        {
                            Cursor.Current = Cursors.WaitCursor;
                            _ESP.DeleteDir(FullDirToDelete);
                            Cursor.Current = Cursors.Default;
                            RefreshFileList();
                        }
                    }
                }
                else 
                {
                    string FileToDelete = (_CurrentPath == "") ? selectedItem : _CurrentPath + "/" + selectedItem;
                    if (MessageBox.Show("Are you sure you want to delete '" + FileToDelete + "'?", "Confirm Delete", MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        _ESP.DeleteFile(FileToDelete);
                        Cursor.Current = Cursors.Default;
                        RefreshFileList();
                    }
                }
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string newFile = openFileDialog1.FileName;
                string newFilename = Path.GetFileName(newFile);
                string FileToAdd = (_CurrentPath == "") ? newFilename : _CurrentPath + "/" + newFilename;
                Cursor.Current = Cursors.WaitCursor;
                _ESP.PutFile(newFile, FileToAdd);
                Cursor.Current = Cursors.Default;
                RefreshFileList();
            }
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            string FileToMove = "";
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (!selectedItem.StartsWith(LBracket))
                {
                    FileToMove = (_CurrentPath == "") ? selectedItem : _CurrentPath + "/" + selectedItem;
                }
                else
                    MessageBox.Show("Can only move files.", "Not Supported");
            }

            if (FileToMove != "")
            {
                string filename = Microsoft.VisualBasic.Interaction.InputBox("New Path and Filename:", "Move File", "");
                if (filename != "")
                {
                    if (filename.IndexOf(".") > 0)
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        _ESP.MoveFile(FileToMove, filename);
                        Cursor.Current = Cursors.Default;
                        RefreshFileList();
                    }
                    else
                        MessageBox.Show("Filename must have an extension.");
                }
            }
        }

        private void btnMkdir_Click(object sender, EventArgs e)
        {
            string newdir = Microsoft.VisualBasic.Interaction.InputBox("New directory under " + lblCurrentDirectory.Text + ":", "Create Directory", "");
            if (newdir != "")
            {
                if (!newdir.Contains("."))
                {
                    string newdirfull = (_CurrentPath == "") ? newdir : _CurrentPath + "/" + newdir;
                    Cursor.Current = Cursors.WaitCursor;
                    _ESP.CreateDir(newdirfull);
                    Cursor.Current = Cursors.Default;
                    RefreshFileList();
                }
                else
                    MessageBox.Show("Cannot create new directory with a period in the name.");
            }
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Backup all files?", "Confirm", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Backup();
                MessageBox.Show("Backup complete.");
            }
        }

        private void scintilla1_TextChanged(object sender, EventArgs e)
        {
            _FileDirty = true;
        }

        private void Manager_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !OKToContinue();
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    _readBuffer = serialPort1.ReadExisting();
                    this.Invoke(new EventHandler(DoUpdate));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "serialPort1_DataReceived() Error");
            }
        }

        private void btnControlC_Click(object sender, EventArgs e)
        {
            InvokeControlC();
            txtTerminal.Focus();
        }

        private void btnControlD_Click(object sender, EventArgs e)
        {
            InvokeControlD();
            txtTerminal.Focus();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void tmrCommStatus_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen && picCommStatus.BackColor != Color.Green)
            {
                picCommStatus.BackColor = Color.Green;
            }
            else if (!serialPort1.IsOpen && picCommStatus.BackColor != Color.Red)
            { 
                picCommStatus.BackColor = Color.Red;
            }

            if (_FileDirty && lblCurrentFile.ForeColor != Color.Red)
                lblCurrentFile.ForeColor = Color.Red;
            else if (!_FileDirty && lblCurrentFile.ForeColor == Color.Red)
                lblCurrentFile.ForeColor = Color.Black;            
        }

        private void btnChangeMode_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                RefreshFileList();
            else
            {
                if (!_externalTerminal)
                {
                    OpenComm();
                    txtTerminal.Focus();
                }
                else
                {
                    Process p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                    p.StartInfo.FileName = _terminalApp;
                    p.StartInfo.Arguments = _terminalAppArgs.Replace("{PORT}", _ESP.COMM_PORT).Replace("{PORTNUM}", Convert.ToInt16(_ESP.COMM_PORT.Replace("COM", "")).ToString());
                    p.Start();
                    //string title = GetCaptionOfActiveWindow();
                    //while (title.Contains("Ampy"))
                    //{
                    //    Application.DoEvents();
                    //    title = GetCaptionOfActiveWindow();
                    //}
                    //SendKeys.SendWait("{ENTER}");
                    p.WaitForExit();
                }
            }
        }

        private void btnLoadHelp_Click(object sender, EventArgs e)
        {
            LoadHelp();
        }

        private void txtTerminal_Enter(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
                OpenComm();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (selectedItem.Substring(0, 1) != LBracket)
                {
                    if (selectedItem.ToLower().EndsWith(".py"))
                    {
                        _runCommand = "import " + selectedItem.Substring(0, selectedItem.Length - 3);
                        if (_externalTerminal)
                        {
                            Process p = new Process();
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                            p.StartInfo.FileName = _terminalApp;
                            p.StartInfo.Arguments = _terminalAppArgs.Replace("{PORT}", _ESP.COMM_PORT);
                            p.Start();
                            string title = GetCaptionOfActiveWindow();
                            while (title.Contains("Ampy"))
                            {
                                Application.DoEvents();
                                title = GetCaptionOfActiveWindow();
                            }
                            SendKeys.SendWait("{ENTER}" + _runCommand + "{ENTER}");
                            p.WaitForExit();
                        }
                        else
                        {
                            OpenComm();
                            txtTerminal.Focus();
                            tmrRunCommand.Enabled = true;
                        }
                    }
                }
            }
        }

        private void txtTerminal_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    char[] key = new char[1];
                    key[0] = e.KeyChar;
                    serialPort1.Write(key, 0, 1);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void tmrRunCommand_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen && _runCommand != "")
            {
                serialPort1.WriteLine(_runCommand);
                _runCommand = "";
                tmrRunCommand.Enabled = false;
            }
        }

        private void txtTerminal_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                e.IsInputKey = true;
        }

        private void txtTerminal_KeyDown(object sender, KeyEventArgs e)
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
            else if ((e.KeyCode == Keys.V && e.Control) || (e.KeyCode == Keys.Insert && e.Shift))
            {
                serialPort1.Write(Clipboard.GetText());
            }
        }

        private void btnCustom_Click(object sender, EventArgs e)
        {
            string keysequence = Microsoft.VisualBasic.Interaction.InputBox("Key Sequence:", "Custom", "");
            if (keysequence != "")
            {
                string[] items = keysequence.Split(',');
                byte[] b = { 0x00 };
                foreach (string item in items)
                {
                    b[0] = Convert.ToByte(item);
                    serialPort1.Write(b, 0, 1);
                }
            }
        }

        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            serialPort1.Write(Clipboard.GetText());
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string FileToExport = "";
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (!selectedItem.StartsWith(LBracket))
                {
                    FileToExport = (_CurrentPath == "") ? selectedItem : _CurrentPath + "/" + selectedItem;
                }
                else
                    MessageBox.Show("Can only export files.", "Not Supported");
            }

            if (FileToExport != "")
            {
                saveFileDialog1.FileName = selectedItem;
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    CloseComm();
                    Cursor.Current = Cursors.WaitCursor;
                    _ESP.GetFile(FileToExport, saveFileDialog1.FileName);
                    Cursor.Current = Cursors.Default;
                }
            }
        }

        private void cboHelp_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadHelp();
        }

        private void btnReplaceAll_Click(object sender, EventArgs e)
        {
            ReplaceAllForm replaceAll = new ReplaceAllForm();
            if (replaceAll.ShowDialog() == DialogResult.OK)
            {
                string FindText = Decode(((TextBox)replaceAll.Controls["txtFind"]).Text);
                string ReplaceText = Decode(((TextBox)replaceAll.Controls["txtReplace"]).Text);

                if (FindText != "")
                    scintilla1.Text = scintilla1.Text.Replace(FindText, ReplaceText);
            }
        }

        #endregion

        #region Private Helper Routines

        private string Decode(string codedString)
        {
            string result = codedString;

            result = result.Replace("\\n", "\n");
            result = result.Replace("\\r", "\r");
            result = result.Replace("\\t", "\t");

            return result;
        }

        private void LoadHelp()
        {
            int current = cboHelp.SelectedIndex;
            if (current >= 0)
            {
                string link = ConfigurationManager.AppSettings["HelpLink" + (current + 1).ToString()];
                if (!link.ToLower().StartsWith("http"))
                {
                    if (link.Contains("\\"))
                        link = "file:///" + link;
                    else
                    {
                        link = "file:///" + Path.Combine(Directory.GetCurrentDirectory(), "help") + "\\" + link;
                    }
                }
                HelpForm help = new HelpForm();
                help.Text = ConfigurationManager.AppSettings["HelpTitle" + (current + 1).ToString()];
                ((WebBrowser)help.Controls["webBrowser1"]).Url = new Uri(link);
                help.Show();
            }
        }

        private void InvokeControlC()
        {
            try
            {
                //if (!serialPort1.IsOpen)
                //{
                //    serialPort1.PortName = _ESP.COMM_PORT;
                //    serialPort1.NewLine = CR;
                //    serialPort1.Open();
                //}

                if (serialPort1.IsOpen)
                {
                    byte[] b = { 0x03 };
                    serialPort1.Write(b, 0, 1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void InvokeControlD()
        {
            try
            {
                //if (!serialPort1.IsOpen)
                //{
                //    serialPort1.PortName = _ESP.COMM_PORT;
                //    serialPort1.NewLine = CR;
                //    serialPort1.Open();
                //}

                if (serialPort1.IsOpen)
                {
                    byte[] b = { 0x04 };
                    serialPort1.Write(b, 0, 1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void LoadHelpDropdown()
        {
            cboHelp.Items.Clear();
            int count = Convert.ToInt32(ConfigurationManager.AppSettings["HelpLinkCount"]);
            if (count == 0)
            {
                cboHelp.Visible = false;
                btnLoadHelp.Visible = false;
            }
            else
            {
                int current = 1;
                while (current <= count)
                {
                    string title = ConfigurationManager.AppSettings["HelpTitle" + current.ToString()];
                    cboHelp.Items.Add(title);
                    current += 1;
                }
            }
        }

        private void ButtonSave()
        {
            bool doRefresh = (_CurrentFile == NEW_FILENAME);
            bool saved = DoSave();
            if (saved)
            {
                _FileDirty = false;
                if (doRefresh)
                    RefreshFileList();
            }
        }

        private bool OKToContinue()
        {
            bool result = true;

            if (_FileDirty)
            {
                DialogResult r = MessageBox.Show("File has been edited.  Do you wish to save it first?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (r == DialogResult.Yes)
                    result = DoSave();
                else if (r == DialogResult.Cancel)
                    result = false;
            }

            return result;
        }

        private bool DoSave(string prefill = "")
        {
            bool result = false;

            if (_CurrentFile == NEW_FILENAME)
            {
                string justfile = prefill;
                if ((prefill.IndexOf('/') >= 0) && (prefill.LastIndexOf('/') < prefill.Length - 1))
                {
                    justfile = prefill.Substring(prefill.LastIndexOf('/') + 1);
                }
                string filename = Microsoft.VisualBasic.Interaction.InputBox("New Filename:", "Save File", justfile);
                if (filename != "")
                {
                    if (filename.IndexOf(".") > 0)
                    {
                        _CurrentFile = _CurrentPath + "/" + filename;
                        result = SaveItem();
                    }
                    else
                        MessageBox.Show("Filename must have an extension.");
                }
            }
            else
            {                
                result = SaveItem();
            }

            return result;
        }

        private void ResetNew()
        {
            scintilla1.Text = "";
            _CurrentFile = NEW_FILENAME;
            _FileDirty = false;
            lblCurrentFile.Text = _CurrentFile;
        }

        private void OpenItem()
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (selectedItem == LBracket + ".." + RBracket) // Go up one directory
                {
                    int lastslash = _CurrentPath.LastIndexOf("/");
                    if (lastslash == 0)
                        _CurrentPath = "";
                    else
                        _CurrentPath = _CurrentPath.Substring(0, lastslash);
                    RefreshFileList();
                }
                else if (selectedItem.Substring(0, 1) == LBracket) // Go into the directory
                {
                    _CurrentPath = _CurrentPath + "/" + selectedItem.Replace(LBracket, "").Replace(RBracket, "");
                    RefreshFileList();
                }
                else // Otherwise open the file
                {
                    _CurrentFile = (_CurrentPath == "") ? selectedItem : _CurrentPath + "/" + selectedItem;
                    string LocalFile = Path.Combine(_SessionPath, selectedItem);

                    if (EditableFile(LocalFile))
                    {
                        CloseComm();
                        Cursor.Current = Cursors.WaitCursor;
                        _ESP.GetFile(_CurrentFile, LocalFile);
                        Cursor.Current = Cursors.Default;
                        if (File.Exists(LocalFile))
                        {
                            using (StreamReader sr = new StreamReader(LocalFile))
                            {
                                string contents = sr.ReadToEnd();
                                scintilla1.Text = contents.Replace(CRLF, LF);
                            }
                            _FileDirty = false;
                            lblCurrentFile.Text = GetFileOnly(_CurrentFile);
                        }
                    }
                    else
                        MessageBox.Show("Not listed as an editable file type.  See the .config file to add more extensions.");
                }
            }
        }

        private bool SaveItem()
        {
            bool result = false;

            try
            {
                string CurrentFilename = _CurrentFile.Substring(_CurrentFile.LastIndexOf('/') + 1);

                string SaveFile = Path.Combine(_SessionPath, CurrentFilename);
                if (File.Exists(SaveFile))
                    File.Delete(SaveFile);

                string text = scintilla1.Text;
                if (text.Contains("\n"))
                    text = text.Replace("\r", "");
                else
                    text = text.Replace("\r", "\n");
                File.WriteAllText(SaveFile, text);

                CloseComm();

                Cursor.Current = Cursors.WaitCursor;

                _ESP.PutFile(SaveFile, _CurrentFile);

                Cursor.Current = Cursors.Default;

                _FileDirty = false;

                result = true;

                MessageBox.Show("File Saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed");
            }

            return result;
        }

        private void RefreshFileList()
        {
            lstDirectory.Items.Clear();

            if (!(_CurrentPath == "" || _CurrentPath == "/"))
                lstDirectory.Items.Add(LBracket + ".." + RBracket);

            CloseComm();

            Cursor.Current = Cursors.WaitCursor;

            List<string> dir = null;

            bool passed = false;
            try
            {
                dir = _ESP.GetDir(_CurrentPath, LBracket, RBracket);
                passed = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RefreshFileList() Exception: " + ex.Message);
            }

            if (!passed)
            {
                Application.DoEvents();
                dir = _ESP.GetDir(_CurrentPath, LBracket, RBracket);
            }

            Cursor.Current = Cursors.Default;

            foreach (string entry in dir)
                lstDirectory.Items.Add(entry);

            lblCurrentDirectory.Text = (_CurrentPath == "") ? "/" : _CurrentPath;

            AllowEditing();
        }

        private void Backup()
        {
            Cursor.Current = Cursors.WaitCursor;

            CloseComm();

            string newBackupPath = Path.Combine(_BackupPath, DateTime.Now.ToString("ByyyyMMdd-HHmm"));
            Directory.CreateDirectory(newBackupPath);

            string BackupCommand = Path.Combine(newBackupPath, "backup.bat");
            MakeBackupScript(BackupCommand);

            string RestoreCommand = Path.Combine(newBackupPath, "restore.bat");
            MakeRestoreScript(RestoreCommand);

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            p.StartInfo.WorkingDirectory = newBackupPath;
            p.StartInfo.FileName = BackupCommand;
            p.Start();
            p.WaitForExit();

            CleanPath(newBackupPath);

            Cursor.Current = Cursors.Default;
        }

        private void MakeBackupScript(string output)
        {
            List<string> files = new List<string>();
            addfiles("/", ref files, true);
            string msg = "";            
            foreach (string item in files)
            {
                if (item.StartsWith(LBracket))
                {
                    msg += item.Substring(1, item.Length - 2) + CRLF;
                }
                else
                {
                    if (EditableFile(item))
                    {
                        string revitem = item.Replace("/", "\\");
                        msg += "ampy -p " + _ESP.COMM_PORT + " -b " + _ESP.BAUD_RATE.ToString() + " get " + item + " " + revitem.Substring(1) + CRLF;
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.Write(msg);
            }
        }

        private void MakeRestoreScript(string output)
        {
            List<string> files = new List<string>();
            addfiles("/", ref files, false);
            string msg = "";
            foreach (string item in files)
            {
                if (item.StartsWith(LBracket))
                {
                    msg += "ampy -p " + _ESP.COMM_PORT + " -b " + _ESP.BAUD_RATE.ToString() + " " + item.Substring(1, item.Length - 2) + CRLF;
                }
                else
                {
                    string revitem = item.Replace("/", "\\");
                    msg += "ampy -p " + _ESP.COMM_PORT + " -b " + _ESP.BAUD_RATE.ToString() + " put " + revitem.Substring(1) + " " + item + CRLF;
                }
            }
            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.Write(msg);
            }
        }

        private void addfiles(string path, ref List<string> files, bool forBackup)
        {
            List<string> items = _ESP.GetDir(path, LBracket, RBracket);
            foreach (string item in items)
                if (!item.StartsWith(LBracket))
                {
                    if (path.EndsWith("/"))
                        files.Add(path + item);
                    else
                        files.Add(path + "/" + item);
                }
            foreach (string item in items)
                if (item.StartsWith(LBracket))
                {
                    string newdir = item.Substring(1, item.Length - 2);
                    string newpath = path;
                    if (newpath.EndsWith("/"))
                        newpath += newdir;
                    else
                        newpath += "/" + newdir;

                    if (forBackup)
                    {
                        int dirCount = 0;
                        if (path != "/")
                        {
                            string[] dirs = path.Substring(1).Split('/');
                            foreach (string dir in dirs)
                                files.Add(LBracket + "cd " + dir + RBracket);
                            dirCount = dirs.Length;
                        }
                        files.Add(LBracket + "mkdir " + newdir + RBracket);
                        if (path != "/")
                        {
                            for (int i = 1; i <= dirCount; i++) 
                                files.Add(LBracket + "cd .." + RBracket);
                        }
                    }
                    else
                    {
                        files.Add(LBracket + "mkdir " + newpath.Substring(1) + RBracket);
                    }

                    addfiles(newpath, ref files, forBackup);
                }
        }

        private void CloseComm()
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                while (serialPort1.IsOpen)
                    Application.DoEvents();

                picCommStatus.BackColor = Color.Red;
                btnChangeMode.Text = "REPL";
                btnChangeMode.ForeColor = Color.Red;
            }
        }

        private void OpenComm()
        {
            if (!serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.PortName = _ESP.COMM_PORT;
                    serialPort1.NewLine = CR;
                    serialPort1.Open();
                    while (!serialPort1.IsOpen)
                        Application.DoEvents();

                    if (serialPort1.IsOpen)
                    {
                        _JustOpened = true;

                        picCommStatus.BackColor = Color.Green;
                        btnChangeMode.Text = "Editor     ";
                        btnChangeMode.ForeColor = Color.Green;
                        FreezeEditing();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        public void DoUpdate(object sender, System.EventArgs e)
        {
            if (!String.IsNullOrEmpty(_readBuffer))
            {
                // remove any jibberish when opening the port
                if (_JustOpened)
                {
                    string markertext = ConfigurationManager.AppSettings["MarkerText"];
                    int goodPos = _readBuffer.IndexOf(markertext);
                    if (goodPos > 0)
                    {
                        _readBuffer = _readBuffer.Substring(goodPos);
                        _JustOpened = false;
                    }
                    else
                    {
                        _readBuffer = "";
                    }
                }

                // check that we have something to process
                if (_readBuffer != "")
                {
                    // process a single backspace
                    if (_readBuffer == "\b\u001b[K")
                    {
                        txtTerminal.SelectionStart = txtTerminal.Text.Length - 1;
                        txtTerminal.SelectionLength = 1;
                        txtTerminal.SelectedText = "";
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
                                txtTerminal.SelectionStart = txtTerminal.Text.Length - count;
                                txtTerminal.SelectionLength = count;
                                txtTerminal.SelectedText = "";
                            }
                            string remainder = cmd.Substring(pos + 1);
                            if (remainder != "")
                            {
                                //MessageBox.Show(remainder);
                                if (remainder == "\b\u001b[K")
                                {
                                    txtTerminal.SelectionStart = txtTerminal.Text.Length - 1;
                                    txtTerminal.SelectionLength = 1;
                                    txtTerminal.SelectedText = "";
                                }
                                else
                                {
                                    if (remainder[0] == 27 && remainder[1] == 91 && remainder[2] == 75)
                                    {
                                        txtTerminal.SelectionStart = txtTerminal.Text.Length;
                                        txtTerminal.SelectionLength = 0;
                                        txtTerminal.SelectedText = remainder.Substring(3);
                                    }
                                    else
                                    {
                                        txtTerminal.AppendText(remainder);
                                        txtTerminal.SelectionStart = txtTerminal.Text.Length;
                                        txtTerminal.SelectionLength = 0;
                                        txtTerminal.ScrollToCaret();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (cmd == "K")
                            {
                                txtTerminal.SelectionStart = txtTerminal.Text.Length - 1;
                                txtTerminal.SelectionLength = 1;
                                txtTerminal.SelectedText = "";
                            }
                            else
                                MessageBox.Show(cmd);
                        }
                    }
                    else // else it is just some text from the device
                    {
                        txtTerminal.AppendText(_readBuffer);
                        txtTerminal.SelectionStart = txtTerminal.Text.Length;
                        txtTerminal.SelectionLength = 0;
                        txtTerminal.ScrollToCaret();
                    }
                }

                // truncate the terminal buffer
                if (txtTerminal.TextLength > _bufferLimit)
                {
                    txtTerminal.Text = txtTerminal.Text.Substring(txtTerminal.TextLength - _bufferResetSize);
                }

            }
        }

        private void FreezeEditing()
        {
            btnRefresh.Enabled = false;
            btnMkdir.Enabled = false;
            btnBackup.Enabled = false;
            btnOpen.Enabled = false;
            btnNew.Enabled = false;
            btnMove.Enabled = false;
            btnDelete.Enabled = false;
            btnLoad.Enabled = false;
            btnExport.Enabled = false;
            btnSave.Enabled = false;
            btnSaveAs.Enabled = false;
            btnReplaceAll.Enabled = false;
            lstDirectory.Enabled = false;
            scintilla1.ReadOnly = true;
            //cboHelp.Enabled = false;
            //btnLoadHelp.Enabled = false;
            btnRun.Enabled = false;
        }

        private void AllowEditing()
        {
            btnRefresh.Enabled = true;
            btnMkdir.Enabled = true;
            btnBackup.Enabled = true;
            btnOpen.Enabled = true;
            btnNew.Enabled = true;
            btnMove.Enabled = true;
            btnDelete.Enabled = true;
            btnLoad.Enabled = true;
            btnExport.Enabled = true;
            btnSave.Enabled = true;
            btnSaveAs.Enabled = true;
            btnReplaceAll.Enabled = true;
            lstDirectory.Enabled = true;
            scintilla1.ReadOnly = false;
            //cboHelp.Enabled = true;
            //btnLoadHelp.Enabled = true;
            btnRun.Enabled = true;
        }

         private string GetFileOnly(string Filename)
        {
            string result = Filename;
            if (result.IndexOf("/") >= 0)
            {
                int pos = result.LastIndexOf("/");
                result = result.Substring(pos + 1);
            }
            return result;
        }

        private bool EditableFile(string Filename)
        {
            bool result = false;

            string[] extensions = _EditableExtensions.ToLower().Split(',');

            string targetExtension = Path.GetExtension(Filename).ToLower();
            if (!String.IsNullOrEmpty(targetExtension))
                targetExtension = targetExtension.Substring(1);

            foreach (string extension in extensions)
            {
                if (extension == targetExtension)
                {
                    result = true;
                    break;
                }
            }
                
            return result;
        }

        private void CleanFile(string FileToClean)
        {
            if (File.Exists(FileToClean))
            {
                string text = File.ReadAllText(FileToClean);
                if (text.Contains("\n"))
                    text = text.Replace("\r", "");
                else
                    text = text.Replace("\r", "\n");
                File.WriteAllText(FileToClean, text);
            }
        }

        private void CleanPath(string RootPath)
        {
            string[] files = Directory.GetFiles(RootPath);
            foreach (string file in files)
            {
                if (EditableFile(file))
                    CleanFile(file);
            }
            string[] dirs = Directory.GetDirectories(RootPath);
            foreach (string dir in dirs)
                CleanPath(dir);
        }

        private void ConfigureForPython(Scintilla scintilla)
        {
            // Reset the styles
            scintilla.StyleResetDefault();
            string EditorFont = ConfigurationManager.AppSettings["EditorFont"];
            if (!String.IsNullOrEmpty(EditorFont))
                scintilla.Styles[Style.Default].Font = EditorFont;
            else
                scintilla.Styles[Style.Default].Font = "Consolas";
            string EditorFontSize = ConfigurationManager.AppSettings["EditorFontSize"];
            if (!String.IsNullOrEmpty(EditorFontSize))
                scintilla.Styles[Style.Default].Size = Convert.ToInt32(EditorFontSize);
            else
                scintilla.Styles[Style.Default].Size = 10;
            scintilla.StyleClearAll(); // i.e. Apply to all

            // Set the lexer
            scintilla.Lexer = Lexer.Python;

            // Known lexer properties:
            // "tab.timmy.whinge.level",
            // "lexer.python.literals.binary",
            // "lexer.python.strings.u",
            // "lexer.python.strings.b",
            // "lexer.python.strings.over.newline",
            // "lexer.python.keywords2.no.sub.identifiers",
            // "fold.quotes.python",
            // "fold.compact",
            // "fold"

            // Some properties we like
            scintilla.SetProperty("tab.timmy.whinge.level", "1");
            scintilla.SetProperty("fold", "1");

            scintilla1.Margins[0].Width = 25;
            scintilla1.Margins[0].Type = MarginType.Number;

            // Use margin 2 for fold markers
            scintilla.Margins[2].Type = MarginType.Symbol;
            scintilla.Margins[2].Mask = Marker.MaskFolders;
            scintilla.Margins[2].Sensitive = true;
            scintilla.Margins[2].Width = 20;

            // Reset folder markers
            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                scintilla.Markers[i].SetForeColor(SystemColors.ControlLightLight);
                scintilla.Markers[i].SetBackColor(SystemColors.ControlDark);
            }

            // Style the folder markers
            scintilla.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            scintilla.Markers[Marker.Folder].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            scintilla.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            scintilla.Markers[Marker.FolderEnd].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            scintilla.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            scintilla.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            scintilla.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            // Enable automatic folding
            scintilla.AutomaticFold = (AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change);

            // Set the styles
            scintilla.Styles[Style.Python.Default].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.CommentLine].ForeColor = Color.FromArgb(0x60, 0x60, 0x60);
            scintilla.Styles[Style.Python.CommentLine].Italic = true;
            scintilla.Styles[Style.Python.Number].ForeColor = Color.FromArgb(0x00, 0x7F, 0x7F);
            scintilla.Styles[Style.Python.String].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Character].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Word].ForeColor = Color.FromArgb(0x00, 0x00, 0x00);
            scintilla.Styles[Style.Python.Word].Bold = true;
            scintilla.Styles[Style.Python.Triple].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            scintilla.Styles[Style.Python.TripleDouble].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            scintilla.Styles[Style.Python.ClassName].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.ClassName].Bold = true;
            scintilla.Styles[Style.Python.DefName].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.DefName].Bold = true;
            scintilla.Styles[Style.Python.Operator].Bold = true;
            scintilla.Styles[Style.Python.Identifier].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.CommentBlock].ForeColor = Color.FromArgb(0x60, 0x60, 0x60);
            scintilla.Styles[Style.Python.CommentBlock].Italic = true;
            scintilla.Styles[Style.Python.StringEol].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.StringEol].BackColor = Color.FromArgb(0xE0, 0xC0, 0xE0);
            scintilla.Styles[Style.Python.StringEol].Bold = true;
            scintilla.Styles[Style.Python.StringEol].FillLine = true;
            scintilla.Styles[Style.Python.Word2].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Decorator].ForeColor = Color.FromArgb(0x80, 0x50, 0x00);

            // Important for Python
            scintilla.ViewWhitespace = WhitespaceMode.VisibleAlways;

            // Keyword lists:
            // 0 "Keywords",
            // 1 "Highlighted identifiers"

            //var python2 = "and as assert break class continue def del elif else except exec finally for from global if import in is lambda not or pass print raise return try while with yield";
            var python3 = "False None True and as assert break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield";
            //var cython = "cdef cimport cpdef";

            scintilla.SetKeywords(0, python3);
            // scintilla.SetKeywords(1, "add your own keywords here");
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        private string GetCaptionOfActiveWindow()
        {
            var strTitle = string.Empty;
            var handle = GetForegroundWindow();
            // Obtain the length of the text   
            var intLength = GetWindowTextLength(handle) + 1;
            var stringBuilder = new StringBuilder(intLength);
            if (GetWindowText(handle, stringBuilder, intLength) > 0)
            {
                strTitle = stringBuilder.ToString();
            }
            return strTitle;
        }

        #endregion

    }
}
