using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public class ESPRoutines
    {
        public string COMM_PORT = "";
        public int BAUD_RATE = 115200;
        public ESPRoutines()
        {
            string[] ports = SerialPort.GetPortNames();

            List<string> goodPorts = new List<string>();
            string exclusions = ConfigurationManager.AppSettings["CommPortExclusions"];
            string[] nums = exclusions.Split(',');
            foreach (string port in ports)
            {
                bool excluded = false;
                foreach (string portnum in nums)
                {
                    if (port == "COM" + portnum)
                    {
                        excluded = true;
                        break;
                    }
                }
                if (!excluded)
                    goodPorts.Add(port);
            }

            COMM_PORT = ConfigurationManager.AppSettings["CommPort"];
            if (!String.IsNullOrEmpty(COMM_PORT))
            {
                bool found = false;
                foreach (string port in goodPorts)
                {
                    if (port == COMM_PORT)
                        found = true;
                }
                if (!found)
                {
                    SelectComForm s = new SelectComForm();
                    ((ComboBox)s.Controls["cboPorts"]).Items.Clear();
                    foreach (string port in goodPorts.OrderBy(g => Convert.ToInt32(g.Substring(3))).ToArray())
                        ((ComboBox)s.Controls["cboPorts"]).Items.Add(port);
                    s.ShowDialog();
                    COMM_PORT = s.SELECTED_COMM_PORT;
                    s.Dispose();
                }
            }
            else
            {
                if (goodPorts.Count() == 1)
                    COMM_PORT = goodPorts[0];
                else
                {
                    SelectComForm s = new SelectComForm();
                    ((ComboBox)s.Controls["cboPorts"]).Items.Clear();
                    foreach (string port in goodPorts.OrderBy(g => Convert.ToInt32(g.Substring(3))).ToArray())
                        ((ComboBox)s.Controls["cboPorts"]).Items.Add(port);
                    s.ShowDialog();
                    COMM_PORT = s.SELECTED_COMM_PORT;
                    s.Dispose();
                }
            }

            string baudratestr = ConfigurationManager.AppSettings["BaudRate"];
            if (baudratestr != "")
                BAUD_RATE = Convert.ToInt32(baudratestr);
        }

        public void PutFile(string SrcFile, string DstFile)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " put \"" + SrcFile + "\" " + DstFile;
            p.Start();
            p.WaitForExit();
        }

        public void GetFile(string espfile, string localfile)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " get " + espfile + " \"" + localfile + "\"";
            p.Start();
            string errors = p.StandardError.ReadToEnd();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
        }

        public void MoveFile(string SrcFile, string DestFile)
        {
            string tmpFile = Path.GetTempFileName();
            GetFile(SrcFile, tmpFile);
            DeleteFile(SrcFile);
            PutFile(tmpFile, DestFile);
        }

        public void DeleteFile(string DeleteFile)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " rm " + DeleteFile;
            p.Start();
            p.WaitForExit();
        }

        public void CreateDir(string NewDirectory)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " mkdir " + NewDirectory;
            p.Start();
            p.WaitForExit();
        }

        public void DeleteDir(string DirectoryToDelete)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " rmdir " + DirectoryToDelete;
            p.Start();
            p.WaitForExit();
        }

        public string GetFileText(string file)
        {
            string contents = "";

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " get " + file;
            p.Start();
            contents = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return contents.Replace("\r\r", "");
        }

        public List<string> GetDir(string path, string LB, string RB)
        {
            List<string> dir = new List<string>();

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "ampy";
            string nav_path = ((!String.IsNullOrEmpty(path)) ? " " + path : "");
            p.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " ls -l" + nav_path;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (output.IndexOf("ESP module with ESP8266") > -1)
            {
                Process p2 = new Process();
                p2.StartInfo.UseShellExecute = false;
                p2.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p2.StartInfo.CreateNoWindow = true;
                p2.StartInfo.RedirectStandardOutput = true;
                p2.StartInfo.FileName = "ampy";
                p2.StartInfo.Arguments = "-p " + COMM_PORT + " -b " + BAUD_RATE.ToString() + " ls -l" + nav_path;
                p2.Start();
                output = p2.StandardOutput.ReadToEnd();
                p2.WaitForExit();
            }

            string[] entries = output.Replace("\r\n", "\t").Split('\t');

            List<string> folders = new List<string>();
            List<string> files = new List<string>();
            foreach (string entry in entries.ToList())
                if (entry != "")
                {
                    string tempstr = "";
                    string filename = "";
                    string size = "";
                    if (nav_path != "")
                        tempstr = entry.Substring(nav_path.Length - 1);
                    else
                        tempstr = entry.Substring(1);
                    int sizepos = tempstr.IndexOf(" - ");
                    if (sizepos > -1)
                    {
                        filename = tempstr.Substring(0, sizepos);
                        size = tempstr.Substring(sizepos + 3);

                        if (filename.StartsWith("/"))
                            filename = filename.Substring(1);

                        if (filename.IndexOf(".") == -1)
                        {
                            folders.Add(filename);
                            if (!(size.StartsWith("0 bytes") || size.StartsWith("10 bytes")))
                                Debug.WriteLine("Folder <" + filename + "> has a file size of <" + size + ">.");
                        }
                        else
                            files.Add(filename);
                    }
                    else
                        Debug.WriteLine("BAD Filename :" + tempstr);
                }

            foreach (string folder in folders.OrderBy(f => f).ToList())
                dir.Add(LB + folder + RB);
            foreach (string file in files.OrderBy(f => f).ToList())
                dir.Add(file);

            return dir;
        }
        
    }
}
