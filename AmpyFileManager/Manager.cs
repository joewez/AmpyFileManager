using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private string _BackupPath = string.Empty;
        private string _SessionPath = string.Empty;
        private string _CurrentPath = string.Empty;
        private string _CurrentFile = string.Empty;
        private string _EditableExtensions = string.Empty;
        private bool _FileDirty = false;
        private string _readBuffer = string.Empty;
        private const string LBracket = "[";
        private const string RBracket = "]";

        private ESPRoutines _ESP;   // Wrapper for AMPY invocations

        public Manager()
        {
            InitializeComponent();            
        }

        #region Events
        private void Manager_Load(object sender, EventArgs e)
        {
            _ESP = new ESPRoutines();
            this.Text = "Ampy File Manager (" + _ESP.COMM_PORT + ")";

            // Get the dir where we save things
            string saveDir = ConfigurationManager.AppSettings["SaveDir"];
            if (String.IsNullOrWhiteSpace(saveDir))
                saveDir = Path.GetDirectoryName(Application.ExecutablePath);

            // directory for all backup files
            _BackupPath = Path.Combine(saveDir, "backups");
            if (!Directory.Exists(_BackupPath))
                Directory.CreateDirectory(_BackupPath);

            // Where we store our files while they are being edited
            string uniqueSessions = ConfigurationManager.AppSettings["UniqueSessions"];
            if (uniqueSessions.Trim().ToUpper().StartsWith("Y"))
                _SessionPath = Path.Combine(_BackupPath, DateTime.Now.ToString("SyyyyMMdd-HHmm"));
            else
                _SessionPath = Path.Combine(saveDir, "session");
            if (!Directory.Exists(_SessionPath))
                Directory.CreateDirectory(_SessionPath);

            // load previous commands
            LoadCommands();

            // load help links
            LoadHelpDropdown();

            // handle the <enter> key when in the cboCommand
            cboCommand.KeyPress += (sndr, ev) =>
            {
                if (ev.KeyChar.Equals((char)13))
                {
                    SendCommand();
                    ev.Handled = true;
                }
            };

            string BaudRate = ConfigurationManager.AppSettings["BaudRate"];
            if (!String.IsNullOrEmpty(BaudRate))
            {
                serialPort1.BaudRate = Convert.ToInt32(BaudRate);
            }

            _EditableExtensions = ConfigurationManager.AppSettings["EditExtensions"];
            if (String.IsNullOrWhiteSpace(_EditableExtensions))
                _EditableExtensions = "py,txt,html,js,css,json";

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
                bool saved = DoSave();
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
                            CloseComm();
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
                        CloseComm();
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
                CloseComm();
                Cursor.Current = Cursors.WaitCursor;
                _ESP.PutFile(newFile, FileToAdd);
                Cursor.Current = Cursors.Default;
                RefreshFileList();
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
                    CloseComm();
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
                    _readBuffer = serialPort1.ReadLine().Replace(CR, CRLF) + CRLF;
                    this.Invoke(new EventHandler(DoUpdate));
                }
            }
            catch (IOException iex)
            {
                Debug.WriteLine(iex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "serialPort1_DataReceived() Error");
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            SendCommand();
        }

        private void btnControlC_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.PortName = _ESP.COMM_PORT;
                    serialPort1.NewLine = CR;
                    serialPort1.Open();
                }

                if (serialPort1.IsOpen)
                {
                    byte[] b = { 0x03 };
                    serialPort1.Write(b, 0, 1);
                }
            }
            catch (IOException iex)
            {
                Debug.WriteLine(iex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "btnControlC_Click() Error");
            }
        }

        private void btnControlD_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.PortName = _ESP.COMM_PORT;
                    serialPort1.NewLine = CR;
                    serialPort1.Open();
                }

                if (serialPort1.IsOpen)
                {
                    byte[] b = { 0x04 };
                    serialPort1.Write(b, 0, 1);
                }
            }
            catch (IOException iex)
            {
                Debug.WriteLine(iex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "btnControlD_Click() Error");
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void tmrCommStatus_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                picCommStatus.BackColor = Color.Green;
            else
                picCommStatus.BackColor = Color.Red;
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
                OpenComm();
        }

        private void Manager_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveCommands();
        }

        private void btnLoadHelp_Click(object sender, EventArgs e)
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
                        link = "file:///" + Directory.GetCurrentDirectory() + "\\" + link;
                    }
                }
                Help help = new Help();
                help.Text = ConfigurationManager.AppSettings["HelpTitle" + (current + 1).ToString()];
                ((WebBrowser)help.Controls["webBrowser1"]).Url = new Uri(link);
                help.Show();
            }
        }

        #endregion

        #region Private Helper Routines

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

        private void LoadCommands()
        {
            string cmdfile = Path.Combine(_SessionPath, "history.txt");
            if (File.Exists(cmdfile))
            {
                using (StreamReader sr = new StreamReader(cmdfile))
                {
                    string line = sr.ReadLine();
                    while (!String.IsNullOrEmpty(line))
                    {
                        cboCommand.Items.Add(line);
                        line = sr.ReadLine();
                    }
                }
            }
        }

        private void SaveCommands()
        {
            string cmdfile = Path.Combine(_SessionPath, "history.txt");
            using (StreamWriter sw = new StreamWriter(cmdfile))
            {
                foreach (var item in cboCommand.Items)
                {
                    string newcmd = item.ToString();
                    if (!String.IsNullOrEmpty(newcmd))
                        sw.WriteLine(newcmd);
                }
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

        private bool DoSave()
        {
            bool result = false;

            if (_CurrentFile == NEW_FILENAME)
            {
                string filename = Microsoft.VisualBasic.Interaction.InputBox("New Filename:", "Save File", "");
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
            if (selectedItem == "")
            {

            }
            else if (selectedItem == LBracket + ".." + RBracket) // Go up one directory
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
            List<string> dir = _ESP.GetDir(_CurrentPath, LBracket, RBracket);
            if (dir.Count == 1 && dir[0].Length > 15)
            {
                dir = _ESP.GetDir(_CurrentPath, LBracket, RBracket);
                Cursor.Current = Cursors.Default;
            }
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
                    string revitem = item.Replace("/", "\\");
                    msg += "ampy -p " + _ESP.COMM_PORT + " get " + item + " " + revitem.Substring(1) + CRLF;
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
                    msg += "ampy -p " + _ESP.COMM_PORT + " " + item.Substring(1, item.Length - 2) + CRLF;
                }
                else
                {
                    string revitem = item.Replace("/", "\\");
                    msg += "ampy -p " + _ESP.COMM_PORT + " put " + revitem.Substring(1) + " " + item + CRLF;
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
                btnChangeMode.Text = "Console Mode";
                btnChangeMode.ForeColor = Color.Red;
            }
        }

        private void OpenComm()
        {
            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = _ESP.COMM_PORT;
                serialPort1.NewLine = CR;
                serialPort1.Open();                
                while (!serialPort1.IsOpen)
                    Application.DoEvents();
                picCommStatus.BackColor = Color.Green;
                btnChangeMode.Text = "Edit Mode   ";
                btnChangeMode.ForeColor = Color.Green;
                FreezeEditing();
            }
        }

        public void DoUpdate(object sender, System.EventArgs e)
        {
            txtTerminal.AppendText(_readBuffer);
            txtTerminal.ScrollToCaret();
        }

        private void FreezeEditing()
        {
            btnRefresh.Enabled = false;
            btnMkdir.Enabled = false;
            btnBackup.Enabled = false;
            btnOpen.Enabled = false;
            btnNew.Enabled = false;
            btnDelete.Enabled = false;
            btnLoad.Enabled = false;
            btnSave.Enabled = false;
            btnSaveAs.Enabled = false;
            lstDirectory.Enabled = false;
            scintilla1.ReadOnly = true;
            cboHelp.Enabled = false;
            btnLoadHelp.Enabled = false;
        }

        private void AllowEditing()
        {
            btnRefresh.Enabled = true;
            btnMkdir.Enabled = true;
            btnBackup.Enabled = true;
            btnOpen.Enabled = true;
            btnNew.Enabled = true;
            btnDelete.Enabled = true;
            btnLoad.Enabled = true;
            btnSave.Enabled = true;
            btnSaveAs.Enabled = true;
            lstDirectory.Enabled = true;
            scintilla1.ReadOnly = false;
            cboHelp.Enabled = true;
            btnLoadHelp.Enabled = true;
        }

        private void SendCommand()
        {
            try
            {
                OpenComm();
                if (serialPort1.IsOpen)
                {
                    string command = cboCommand.Text;
                    serialPort1.WriteLine(command);
                    TestNewCommand(command);
                    cboCommand.Text = "";
                }
            }
            catch (IOException iex)
            {
                Debug.WriteLine(iex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SendCommand() Error");
            }
        }

        private void TestNewCommand(string command)
        {
            if (!String.IsNullOrEmpty(command))
            {
                bool found = false;
                foreach (var item in cboCommand.Items)
                {
                    if (item.ToString() == command)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    cboCommand.Items.Insert(0, command);
            }
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
            scintilla.Styles[Style.Python.Default].ForeColor = Color.FromArgb(0x80, 0x80, 0x80);
            scintilla.Styles[Style.Python.CommentLine].ForeColor = Color.FromArgb(0x00, 0x7F, 0x00);
            scintilla.Styles[Style.Python.CommentLine].Italic = true;
            scintilla.Styles[Style.Python.Number].ForeColor = Color.FromArgb(0x00, 0x7F, 0x7F);
            scintilla.Styles[Style.Python.String].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Character].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Word].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            scintilla.Styles[Style.Python.Word].Bold = true;
            scintilla.Styles[Style.Python.Triple].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            scintilla.Styles[Style.Python.TripleDouble].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            scintilla.Styles[Style.Python.ClassName].ForeColor = Color.FromArgb(0x00, 0x00, 0xFF);
            scintilla.Styles[Style.Python.ClassName].Bold = true;
            scintilla.Styles[Style.Python.DefName].ForeColor = Color.FromArgb(0x00, 0x7F, 0x7F);
            scintilla.Styles[Style.Python.DefName].Bold = true;
            scintilla.Styles[Style.Python.Operator].Bold = true;
            // scintilla.Styles[Style.Python.Identifier] ... your keywords styled here
            scintilla.Styles[Style.Python.CommentBlock].ForeColor = Color.FromArgb(0x7F, 0x7F, 0x7F);
            scintilla.Styles[Style.Python.CommentBlock].Italic = true;
            scintilla.Styles[Style.Python.StringEol].ForeColor = Color.FromArgb(0x00, 0x00, 0x00);
            scintilla.Styles[Style.Python.StringEol].BackColor = Color.FromArgb(0xE0, 0xC0, 0xE0);
            scintilla.Styles[Style.Python.StringEol].FillLine = true;
            scintilla.Styles[Style.Python.Word2].ForeColor = Color.FromArgb(0x40, 0x70, 0x90);
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

        #endregion

    }
}
