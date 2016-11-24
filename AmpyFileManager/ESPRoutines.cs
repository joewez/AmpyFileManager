using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmpyFileManager
{
    public class ESPRoutines
    {
        public string COMM_PORT = "";

        public ESPRoutines()
        {
            string[] ports = SerialPort.GetPortNames();

            COMM_PORT = ConfigurationManager.AppSettings["CommPort"];
            if (!String.IsNullOrEmpty(COMM_PORT))
            {
                bool found = false;
                foreach (string port in ports)
                {
                    if (port == COMM_PORT)
                        found = true;
                }
                if (!found)
                {
                    SelectCom s = new SelectCom();
                    s.ShowDialog();
                    COMM_PORT = ((ComboBox)s.Controls["cboPorts"]).Text;
                    s.Dispose();
                }
            }
            else
            {
                if (ports.Count() == 1)
                    COMM_PORT = ports[0];
                else
                {
                    SelectCom s = new SelectCom();
                    s.ShowDialog();
                    COMM_PORT = ((ComboBox)s.Controls["cboPorts"]).Text;
                    s.Dispose();
                }
                ConfigurationManager.AppSettings["CommPort"] = COMM_PORT;
            }

        }

        public void PutFile(string SrcFile, string DstFile)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " put " + SrcFile + " " + DstFile;
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
            p.StartInfo.Arguments = "-p " + COMM_PORT + " get " + espfile + " " + localfile;
            p.Start();
            string errors = p.StandardError.ReadToEnd();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
        }

        public string RunFile(string RunFile)
        {
            string output = "";
            string errors = "";

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " run " + RunFile;
            p.Start();
            output = p.StandardOutput.ReadToEnd();
            errors = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (output == "" && errors != "")
                return errors.Replace("\r\r", "");
            else
                return output.Replace("\r\r", "");
        }

        public void DeleteFile(string DeleteFile)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " rm " + DeleteFile;
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
            p.StartInfo.Arguments = "-p " + COMM_PORT + " mkdir " + NewDirectory;
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
            p.StartInfo.Arguments = "-p " + COMM_PORT + " get " + file;
            p.Start();
            contents = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return contents.Replace("\r\r", "");
        }

        public List<string> GetDir(string path)
        {
            List<string> dir = new List<string>();

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "ampy";
            p.StartInfo.Arguments = "-p " + COMM_PORT + " ls" + ((!String.IsNullOrEmpty(path)) ? " " + path : "");
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            string[] entries = output.Replace("\r\n", "\t").Split('\t');

            List<string> folders = new List<string>();
            List<string> files = new List<string>();
            foreach (string entry in entries.ToList())
                if (entry != "")
                {
                    if (entry.ToLower().EndsWith(".py") || entry.ToLower().EndsWith(".txt") || entry.IndexOf(".") >= 0)
                        files.Add(entry);
                    else
                        folders.Add(entry);
                }

            foreach (string folder in folders.OrderBy(f => f).ToList())
                dir.Add("<" + folder + ">");
            foreach (string file in files.OrderBy(f => f).ToList())
                dir.Add(file);

            return dir;
        }

    }
}
