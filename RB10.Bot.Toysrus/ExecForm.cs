using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RB10.Bot.Toysrus
{
    public partial class ExecForm : Form
    {
        private class Log
        {
            public string Status { get; set; }
            public string LogDate { get; set; }
            public string JanCode { get; set; }
            public string Message { get; set; }
        }

        private BindingList<Log> _logs { get; set; }
        delegate void LogDelegate(string janCode, string logDate, string status, string message);

        public ExecForm()
        {
            InitializeComponent();
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (JanCodeFileTextBox.Text == "") throw new ApplicationException("JANコードファイルパスを入力してください。");

                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Title = "結果ファイルの出力先を指定して下さい。";
                dlg.FileName = System.IO.Path.GetFileNameWithoutExtension(JanCodeFileTextBox.Text) + "_result.csv";
                if (dlg.ShowDialog() == DialogResult.Cancel) return;

                var task = new ToysrusBot();
                task.ExecutingStateChanged += Task_ExecutingStateChanged;
                task.Start(JanCodeFileTextBox.Text, dlg.FileName);
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Task_ExecutingStateChanged(object sender, ToysrusBot.ExecutingStateEventArgs e)
        {
            Invoke(new LogDelegate(AddLog), e.ReportState.ToString(), DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.JanCode, e.Message);
        }

        private void AddLog(string status, string logDate, string janCode, string message)
        {
            if (_logs == null)
            {
                _logs = new BindingList<Log>();
                dataGridView1.DataSource = _logs;
            }

            _logs.Insert(0, new Log { Status = status, LogDate = logDate, JanCode = janCode, Message = message });
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1) return;

            if (e.ColumnIndex == 0)
            {
                if (e.Value.ToString() == "Warning")
                {
                    DataGridViewCellStyle cellStyle = new DataGridViewCellStyle() { BackColor = System.Drawing.Color.Yellow, ForeColor = System.Drawing.Color.Black };
                    dataGridView1.Rows[e.RowIndex].DefaultCellStyle = cellStyle;
                }
                else if (e.Value.ToString() == "Error")
                {
                    DataGridViewCellStyle cellStyle = new DataGridViewCellStyle() { BackColor = System.Drawing.Color.Red, ForeColor = System.Drawing.Color.White };
                    dataGridView1.Rows[e.RowIndex].DefaultCellStyle = cellStyle;
                }
            }
        }

        private void FileSelectButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "csvファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.Cancel) return;

            JanCodeFileTextBox.Text = dlg.FileName;
        }
    }
}
