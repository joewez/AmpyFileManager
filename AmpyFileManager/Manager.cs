using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ScintillaNET;

namespace AmpyFileManager
{
    public partial class Manager : Form
    {
        private const string NEW_FILENAME = "<new>";
        private const string LF = "\n";
        private const string CRLF = "\r\n";
        private string _BackupPath = "";
        private string _SessionPath = "";
        private string _CurrentPath = "";
        private string _CurrentFile = "";
        private bool _FileDirty = false;
        private string _readBuffer = string.Empty;

        private ESPRoutines _ESP;

        public Manager()
        {
            InitializeComponent();            
        }

        #region Events
        private void Manager_Load(object sender, EventArgs e)
        {
            _ESP = new ESPRoutines();
            this.Text = "Ampy File Manager (" + _ESP.COMM_PORT + ")";

            _BackupPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "backups");
            if (!Directory.Exists(_BackupPath))
                Directory.CreateDirectory(_BackupPath);

            _SessionPath = Path.Combine(_BackupPath, DateTime.Now.ToString("yyyyMMdd-hhmm"));
            Directory.CreateDirectory(_SessionPath);

            txtCommand.KeyPress += (sndr, ev) =>
            {
                if (ev.KeyChar.Equals((char)13))
                {
                    SendCommand();
                    ev.Handled = true;
                }
            };

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
            bool doRefresh = (_CurrentFile == NEW_FILENAME);
            bool saved = DoSave();
            if (saved && doRefresh)
                RefreshFileList();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            if (OKToContinue())
                ResetNew();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "" && selectedItem != "<..>" && selectedItem.Substring(0, 1) != "<")
            {
                string FileToDelete = (_CurrentPath == "") ? selectedItem: _CurrentPath + "/" + selectedItem;
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
                string newdirfull = (_CurrentPath == "") ? newdir : _CurrentPath + "/" + newdir;
                CloseComm();
                Cursor.Current = Cursors.WaitCursor;
                _ESP.CreateDir(newdirfull);
                Cursor.Current = Cursors.Default;
                RefreshFileList();
            }
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "" && selectedItem != "<..>" && selectedItem.Substring(0, 1) != "<")
            {
                CloseComm();
                Cursor.Current = Cursors.WaitCursor;
                string output = _ESP.RunFile(selectedItem);
                Cursor.Current = Cursors.Default;
                MessageBox.Show(output, "Output");
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
                    _readBuffer = serialPort1.ReadLine().Replace("\r", "\r\n") + "\r\n";
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
                    serialPort1.NewLine = "\r";
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
                    serialPort1.NewLine = "\r";
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
                MessageBox.Show(ex.Message, "btnControlC_Click() Error");
            }
        }

        private void picCommStatus_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
                CloseComm();
            else
                OpenComm();
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
        }

        #endregion

        #region Private Helper Routines

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
                    _CurrentFile = _CurrentPath + "/" + filename;                    
                    result = SaveItem();
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

        private bool SaveItem()
        {
            bool result = false;

            try
            {
                string CurrentFilename = _CurrentFile.Substring(_CurrentFile.LastIndexOf('/') + 1);
                string SaveFile = Path.Combine(_SessionPath, CurrentFilename);
                if (File.Exists(SaveFile))
                    File.Delete(SaveFile);

                using (TextWriter tw = new StreamWriter(SaveFile))
                {
                    tw.NewLine = LF;
                    tw.Write(scintilla1.Text.Replace(CRLF, LF));
                }

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
                lstDirectory.Items.Add("<..>");

            CloseComm();
            Cursor.Current = Cursors.WaitCursor;
            List<string> dir = _ESP.GetDir(_CurrentPath);
            Cursor.Current = Cursors.Default;
            foreach (string entry in dir)
                lstDirectory.Items.Add(entry);

            lblCurrentDirectory.Text = (_CurrentPath == "") ? "<root>" : _CurrentPath;
        }


        private void DirectBackup()
        {
            string newBackupPath = Path.Combine(_BackupPath, DateTime.Now.ToString("ByyyyMMdd-hhmm"));
            Directory.CreateDirectory(newBackupPath);

            CloseComm();
            Cursor.Current = Cursors.WaitCursor;
            foreach (string item in lstDirectory.Items)
            {
                if (!item.StartsWith("<"))
                {
                    string currentFile = (_CurrentPath == "") ? item : _CurrentPath + "/" + item;
                    string LocalFile = Path.Combine(newBackupPath, item);
                    _ESP.GetFile(currentFile, LocalFile);
                }
            }
            Cursor.Current = Cursors.Default;
        }

        private void Backup()
        {
            Cursor.Current = Cursors.WaitCursor;

            CloseComm();

            string newBackupPath = Path.Combine(_BackupPath, DateTime.Now.ToString("ByyyyMMdd-hhmm"));
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

            Cursor.Current = Cursors.Default;
        }

        private void MakeBackupScript(string output)
        {
            List<string> files = new List<string>();
            addfiles("/", ref files, true);
            string msg = "";            
            foreach (string item in files)
            {
                if (item.StartsWith("<"))
                {
                    msg += item.Substring(1, item.Length - 2) + "\r\n";
                }
                else
                {
                    string revitem = item.Replace("/", "\\");
                    msg += "ampy -p " + _ESP.COMM_PORT + " get " + item + " " + revitem.Substring(1) + "\r\n";
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
                if (item.StartsWith("<"))
                {
                    msg += "ampy -p " + _ESP.COMM_PORT + " " + item.Substring(1, item.Length - 2) + "\r\n";
                }
                else
                {
                    string revitem = item.Replace("/", "\\");
                    msg += "ampy -p " + _ESP.COMM_PORT + " put " + revitem.Substring(1) + " " + item + "\r\n";
                }
            }
            using (StreamWriter sw = new StreamWriter(output))
            {
                sw.Write(msg);
            }
        }

        private void addfiles(string path, ref List<string> files, bool forBackup)
        {
            List<string> items = _ESP.GetDir(path);
            foreach (string item in items)
                if (!item.StartsWith("<"))
                {
                    if (path.EndsWith("/"))
                        files.Add(path + item);
                    else
                        files.Add(path + "/" + item);
                }
            foreach (string item in items)
                if (item.StartsWith("<"))
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
                                files.Add("<cd " + dir + ">");
                            dirCount = dirs.Length;
                        }
                        files.Add("<mkdir " + newdir + ">");
                        if (path != "/")
                        {
                            for (int i = 1; i <= dirCount; i++) 
                                files.Add("<cd ..>");
                        }
                    }
                    else
                    {
                        files.Add("<mkdir " + newpath.Substring(1) + ">");
                    }

                    addfiles(newpath, ref files, forBackup);
                }
        }

        private void OpenItem()
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem == "<..>") // Go up one directory
            {
                int lastslash = _CurrentPath.LastIndexOf("/");
                if (lastslash == 0)
                    _CurrentPath = "";
                else
                    _CurrentPath = _CurrentPath.Substring(0, lastslash);
                RefreshFileList();
            }
            else if (selectedItem.Substring(0, 1) == "<") // Go into the directory
            {
                _CurrentPath = _CurrentPath + "/" + selectedItem.Replace("<", "").Replace(">", "");
                RefreshFileList();
            }
            else // Otherwise open the file
            {
                _CurrentFile = (_CurrentPath == "") ? selectedItem : _CurrentPath + "/" + selectedItem;
                string LocalFile = Path.Combine(_SessionPath, selectedItem);

                CloseComm();
                Cursor.Current = Cursors.WaitCursor;
                _ESP.GetFile(_CurrentFile, LocalFile);
                Cursor.Current = Cursors.Default;
                if (File.Exists(LocalFile))
                {
                    using (StreamReader sr = new StreamReader(LocalFile))
                    {
                        scintilla1.Text = sr.ReadToEnd().Replace("\r\n", "\n");
                    }
                    _FileDirty = false;
                    lblCurrentFile.Text = _CurrentFile;
                }
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
            }
        }

        private void OpenComm()
        {
            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = _ESP.COMM_PORT;
                serialPort1.NewLine = "\r";
                serialPort1.Open();                
                while (!serialPort1.IsOpen)
                    Application.DoEvents();
                picCommStatus.BackColor = Color.Green;
            }
        }

        private void txtTerminal_Enter(object sender, System.EventArgs e) { }

        private void txtTerminal_Leave(object sender, System.EventArgs e) { }

        public void DoUpdate(object sender, System.EventArgs e)
        {
            txtTerminal.AppendText(_readBuffer);
            txtTerminal.ScrollToCaret();
        }

        private void SendCommand()
        {
            try
            {
                OpenComm();
                if (serialPort1.IsOpen)
                {
                    serialPort1.WriteLine(txtCommand.Text);
                    txtCommand.Text = "";
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

        private void ConfigureForPython(Scintilla scintilla)
        {
            // Reset the styles
            scintilla.StyleResetDefault();
            scintilla.Styles[Style.Default].Font = "Consolas";
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
