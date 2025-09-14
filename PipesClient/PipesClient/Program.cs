using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace Pipes
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            frmMain form = new frmMain();
            form.nickBox.Text =  Dns.GetHostName();
            Application.Run(form);
        }
    }
}
