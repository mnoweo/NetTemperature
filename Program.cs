using NetTemperatureMonitor.UI;
using Sunny.UI;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace NetTemperatureMonitor
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            string processName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 1)
            {
                UIMessageBox.Show("运行程序已存在");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}