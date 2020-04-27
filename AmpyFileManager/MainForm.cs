using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScintillaNET;
using WindowsInput;
using AmpyFileManager.Properties;

namespace AmpyFileManager
{
    public partial class frmMain : Form
    {
        private const string NEW_FILENAME = "<new>";
        private const string LBracket = "[";
        private const string RBracket = "]";
        private const string LF = "\n";
        private const string CRLF = "\r\n";

        private ESPRoutines _ESP;
        private string _SessionPath = string.Empty;
        private string _EditableExtensions = string.Empty;
        private bool _FileDirty = false;
        private string _CurrentPath = string.Empty;
        private string _CurrentFile = string.Empty;

        public frmMain(ESPRoutines ESP)
        {
            _ESP = ESP;
            InitializeComponent();
        }

        #region Event Handlers

        private void frmMain_Load(object sender, EventArgs e)
        {
            this.Text = "Ampy File Manager (" + _ESP.COMM_PORT + ")";

            // Get the dir where we save things
            string saveDir = ConfigurationManager.AppSettings["SaveDir"];
            if (String.IsNullOrWhiteSpace(saveDir))
                saveDir = Path.GetDirectoryName(Application.ExecutablePath);

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
            cboHelp.Items.Clear();
            int count = Convert.ToInt32(ConfigurationManager.AppSettings["HelpLinkCount"]);
            if (count == 0)
                cboHelp.Visible = false;
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

            // Setup tooltips
            toolTip1.SetToolTip(btnREPL, "Go to the MicroPython REPL");
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

            lstDirectory.BackColor = DecodeColor("ExplorerColor");
            lstDirectory.Font = new Font(ConfigurationManager.AppSettings["DirectoryFont"], Convert.ToSingle(ConfigurationManager.AppSettings["DirectoryFontSize"]), FontStyle.Regular);

            ConfigureForPython(scintilla1);

            RefreshFileList();

            ResetNew();

            GetWindowValue();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            if (OKToContinue())
                ResetNew();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (OKToContinue())
                OpenItem();
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
                    Cursor.Current = Cursors.WaitCursor;
                    _ESP.GetFile(FileToExport, saveFileDialog1.FileName);
                    Cursor.Current = Cursors.Default;
                }
            }
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

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            string selectedItem = lstDirectory.Text;
            if (selectedItem != "")
            {
                if (!selectedItem.StartsWith(LBracket))
                {
                    if (selectedItem.EndsWith(".py"))
                        if (_CurrentPath == "/" || _CurrentPath == "")
                            OpenREPL("import " + selectedItem.Replace(".py", ""));
                        else
                            OpenREPL("from " + _CurrentPath.Substring(1) + " import " + selectedItem.Replace(".py", ""));
                    else
                        MessageBox.Show("Can only run '.py' files.", "Not Supported");
                }
                else
                    MessageBox.Show("Can only run '.py' files.", "Not Supported");
            }
        }

        private void cboHelp_SelectedIndexChanged(object sender, EventArgs e)
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

        private void btnREPL_Click(object sender, EventArgs e)
        {
            OpenREPL("");
        }

        private void btnReplaceAll_Click(object sender, EventArgs e)
        {
            ReplaceAllForm replaceAll = new ReplaceAllForm();
            if (replaceAll.ShowDialog() == DialogResult.OK)
            {
                string FindText = Decode(((TextBox)replaceAll.Controls["txtFind"]).Text);
                string ReplaceText = Decode(((TextBox)replaceAll.Controls["txtReplace"]).Text);

                if (FindText != "")
                {
                    scintilla1.Text = scintilla1.Text.Replace(FindText, ReplaceText);
                    MessageBox.Show("Done.", "Global Search And Replace");
                }
            }
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
                    lblCurrentFile.ForeColor = Color.Black;
                    RefreshFileList();
                }
                else
                    _CurrentFile = oldFilename;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            ButtonSave();
        }

        private void scintilla1_TextChanged(object sender, EventArgs e)
        {
            _FileDirty = true;
            lblCurrentFile.ForeColor = Color.Red;
        }

        private void lstDirectory_DoubleClick(object sender, EventArgs e)
        {
            if (OKToContinue())
                OpenItem();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !OKToContinue();
            SaveWindowValue();
        }

        private void tmrMessage_Tick(object sender, EventArgs e)
        {
            pnlSaveMessage.Visible = false;
            tmrMessage.Enabled = false;
        }

        #endregion

        #region Helper Routines

        private void GetWindowValue()
        {
            Width = Settings.Default.WindowWidth;
            Height = Settings.Default.WindowHeight;
            Top = Settings.Default.WindowTop < 0 ? 0 : Settings.Default.WindowTop;
            Left = Settings.Default.WindowLeft < 0 ? 0 : Settings.Default.WindowLeft;
        }

        private void SaveWindowValue()
        {
            Settings.Default.WindowHeight = Height;
            Settings.Default.WindowWidth = Width;
            Settings.Default.WindowLeft = Left;
            Settings.Default.WindowTop = Top;
            Settings.Default.Save();
        }

        private string Decode(string codedString)
        {
            string result = codedString;

            result = result.Replace("\\n", "\n");
            result = result.Replace("\\r", "\r");
            result = result.Replace("\\t", "\t");

            return result;
        }

        private void ButtonSave()
        {
            bool doRefresh = (_CurrentFile == NEW_FILENAME);
            bool saved = DoSave();
            if (saved)
            {
                _FileDirty = false;
                lblCurrentFile.ForeColor = Color.Black;
                if (doRefresh)
                    RefreshFileList();
            }
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
                            lblCurrentFile.ForeColor = Color.Black;
                        }
                    }
                    else
                        MessageBox.Show("Not listed as an editable file type.  See the .config file to add more extensions.");
                }
            }
        }

        private void RefreshFileList()
        {
            lstDirectory.Items.Clear();

            if (!(_CurrentPath == "" || _CurrentPath == "/"))
                lstDirectory.Items.Add(LBracket + ".." + RBracket);

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

            int folderCount = 0;
            int fileCount = 0;
            foreach (string entry in dir)
            {
                lstDirectory.Items.Add(entry);
                if (entry.StartsWith(LBracket) && entry.EndsWith(RBracket))
                    folderCount++;
                else
                    fileCount++;
            }
            lblFolderCount.Text = folderCount.ToString();
            lblFileCount.Text = fileCount.ToString();
            lblCurrentDirectory.Text = (_CurrentPath == "") ? "/" : _CurrentPath;
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

                Cursor.Current = Cursors.WaitCursor;
                _ESP.PutFile(SaveFile, _CurrentFile);
                Cursor.Current = Cursors.Default;

                _FileDirty = false;

                result = true;

                pnlSaveMessage.Top = (scintilla1.Height - pnlSaveMessage.Height) / 2;
                pnlSaveMessage.Left = (scintilla1.Width - pnlSaveMessage.Width) / 2;
                pnlSaveMessage.Visible = true;
                tmrMessage.Enabled = true;
                //MessageBox.Show("File Saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed");
            }

            return result;
        }

        private void ResetNew()
        {
            scintilla1.Text = "";
            _CurrentFile = NEW_FILENAME;
            _FileDirty = false;
            lblCurrentFile.Text = _CurrentFile;
            lblCurrentFile.ForeColor = Color.Black;
        }

        private void OpenREPL(string cmd)
        {
            if (ConfigurationManager.AppSettings["ExternalTerminal"] == "Y")
            {
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                p.StartInfo.FileName = ConfigurationManager.AppSettings["TerminalApp"];
                string terminalAppArgs = ConfigurationManager.AppSettings["TerminalAppArgs"];
                p.StartInfo.Arguments = terminalAppArgs.Replace("{PORT}", _ESP.COMM_PORT).Replace("{PORTNUM}", Convert.ToInt16(_ESP.COMM_PORT.Replace("COM", "")).ToString());
                p.Start();
                
                string terminalAppTitle = ConfigurationManager.AppSettings["TerminalAppTitle"];
                string title = GetCaptionOfActiveWindow();
                while (!title.Contains(terminalAppTitle))
                {
                    Application.DoEvents();
                    title = GetCaptionOfActiveWindow();
                }

                InputSimulator inputSimulator = new InputSimulator();
                KeyboardSimulator keySimulator = new KeyboardSimulator(inputSimulator);
                keySimulator.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                if (!String.IsNullOrEmpty(cmd))
                {
                    keySimulator.TextEntry(cmd);
                    keySimulator.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                }                  

                p.WaitForExit();
            }
            else
            {
                TerminalForm terminal = new TerminalForm(_ESP.COMM_PORT, _ESP.BAUD_RATE, cmd);
                terminal.ShowDialog();
            }
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
            scintilla.Styles[Style.Python.Default].ForeColor = DecodeColor("Python.Default.ForeColor");
            scintilla.Styles[Style.Python.CommentLine].ForeColor = DecodeColor("Python.CommentLine.ForeColor");
            scintilla.Styles[Style.Python.CommentLine].Italic = DecodeBoolean("Python.CommentLine.Italic");
            scintilla.Styles[Style.Python.Number].ForeColor = DecodeColor("Python.Number.ForeColor");
            scintilla.Styles[Style.Python.String].ForeColor = DecodeColor("Python.String.ForeColor");
            scintilla.Styles[Style.Python.Character].ForeColor = DecodeColor("Python.Character.ForeColor");
            scintilla.Styles[Style.Python.Word].ForeColor = DecodeColor("Python.Word.ForeColor");
            scintilla.Styles[Style.Python.Word].Bold = DecodeBoolean("Python.Word.Bold");
            scintilla.Styles[Style.Python.Triple].ForeColor = DecodeColor("Python.Triple.ForeColor");
            scintilla.Styles[Style.Python.TripleDouble].ForeColor = DecodeColor("Python.TripleDouble.ForeColor");
            scintilla.Styles[Style.Python.ClassName].ForeColor = DecodeColor("Python.ClassName.ForeColor");
            scintilla.Styles[Style.Python.ClassName].Bold = DecodeBoolean("Python.ClassName.Bold");
            scintilla.Styles[Style.Python.DefName].ForeColor = DecodeColor("Python.DefName.ForeColor");
            scintilla.Styles[Style.Python.DefName].Bold = DecodeBoolean("Python.DefName.Bold");
            scintilla.Styles[Style.Python.Operator].Bold = DecodeBoolean("Python.Operator.Bold");
            scintilla.Styles[Style.Python.Identifier].ForeColor = DecodeColor("Python.Identifier.ForeColor");
            scintilla.Styles[Style.Python.CommentBlock].ForeColor = DecodeColor("Python.CommentBlock.ForeColor");
            scintilla.Styles[Style.Python.CommentBlock].Italic = DecodeBoolean("Python.CommentBlock.Italic");
            scintilla.Styles[Style.Python.StringEol].ForeColor = DecodeColor("Python.StringEol.ForeColor");
            scintilla.Styles[Style.Python.StringEol].BackColor = DecodeColor("Python.StringEol.BackColor");
            scintilla.Styles[Style.Python.StringEol].Bold = DecodeBoolean("Python.StringEol.Bold");
            scintilla.Styles[Style.Python.StringEol].FillLine = DecodeBoolean("Python.StringEol.FillLine");
            scintilla.Styles[Style.Python.Word2].ForeColor = DecodeColor("Python.Word2.ForeColor");
            scintilla.Styles[Style.Python.Decorator].ForeColor = DecodeColor("Python.Decorator.ForeColor");

            // Important for Python
            scintilla.ViewWhitespace = WhitespaceMode.VisibleAlways;

            // Keyword lists:
            // 0 "Keywords",
            // 1 "Highlighted identifiers"

            //var python2 = "and as assert break class continue def del elif else except exec finally for from global if import in is lambda not or pass print raise return try while with yield";
            //var python3 = "False None True and as assert break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield";
            var micropython = ConfigurationManager.AppSettings["Python.Keywords"];

            scintilla.SetKeywords(0, micropython);
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

        private bool DecodeBoolean(string BooleanSettingName)
        {
            bool result = false;

            string BooleanSetting = ConfigurationManager.AppSettings[BooleanSettingName];
            if (!String.IsNullOrEmpty(BooleanSetting) && BooleanSetting.Trim().ToUpper().Substring(0, 1) == "Y")
            {
                result = true;
            }

            return result;
        }

        #endregion

    }
}
