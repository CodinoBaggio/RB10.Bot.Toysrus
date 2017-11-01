﻿using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RB10.Bot.Toysrus
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ExecForm());
            }
            else
            {
                var task = new ToysrusBot();
                task.Start(Properties.Settings.Default.JanCodeFileName, Properties.Settings.Default.ResultFileName);
            }
        }
    }
}
