using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RB10.Bot.YodobashiCamera
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ExecForm());
            }
            else
            {
                string resultFileName = $"{System.IO.Path.GetFileNameWithoutExtension(Properties.Settings.Default.JanCodeFileName)}_result{DateTime.Now.ToString("yyyyMMddHHmmss")}.csv";

                var task = new YodobashiCameraBot();
                task.Start(Properties.Settings.Default.JanCodeFileName, resultFileName, Properties.Settings.Default.Delay, Properties.Settings.Default.IncludeUnPosted);
            }
        }
    }
}
