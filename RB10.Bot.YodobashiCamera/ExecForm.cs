using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RB10.Bot.YodobashiCamera
{
    public partial class ExecForm : Form
    {
        private class Log
        {
            public string ProcessStatus { get; set; }
            public string Status { get; set; }
            public string LogDate { get; set; }
            public string JanCode { get; set; }
            public string Message { get; set; }
        }

        private BindingList<Log> _logs { get; set; }
        delegate void LogDelegate(string processStatus, string status, string janCode, string logDate, string message);

        public ExecForm()
        {
            InitializeComponent();
        }

        private void ExecForm_Load(object sender, EventArgs e)
        {
            // 画面チラツキ防止（ダブルバッファ設定）
            System.Type dgvtype = typeof(DataGridView);
            System.Reflection.PropertyInfo dgvPropertyInfo = dgvtype.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            dgvPropertyInfo.SetValue(dataGridView1, true, null);
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            try
            {
                //if (JanCodeFileTextBox.Text == "") throw new ApplicationException("JANコードファイルパスを入力してください。");

                //SaveFileDialog dlg = new SaveFileDialog();
                //dlg.Title = "結果ファイルの出力先を指定して下さい。";
                //dlg.Filter = "csvファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*";
                //dlg.FileName = $"{System.IO.Path.GetFileNameWithoutExtension(JanCodeFileTextBox.Text)}_result{DateTime.Now.ToString("yyyyMMddHHmmss")}.csv";
                //if (dlg.ShowDialog() == DialogResult.Cancel) return;

                //dataGridView1.Rows.Clear();

                var task = new YodobashiCameraBot();
                task.ExecutingStateChanged += Task_ExecutingStateChanged;
                task.Research();
                //task.Start(JanCodeFileTextBox.Text, dlg.FileName, (int)DelayNumericUpDown.Value, IncludeUnPostedCheckBox.Checked);
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

        private void Task_ExecutingStateChanged(object sender, YodobashiCameraBot.ExecutingStateEventArgs e)
        {
            Invoke(new LogDelegate(UpdateLog), e.ProcessStatus.ToString(), e.NotifyStatus.ToString(), e.JanCode, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
        }

        private void UpdateLog(string processStatus, string status, string janCode, string logDate, string message)
        {
            if (_logs == null)
            {
                _logs = new BindingList<Log>();
                dataGridView1.DataSource = _logs;
            }

            var log = _logs.Where(x => x.JanCode == janCode);
            if (0 < log.Count())
            {
                log.First().LogDate = logDate;
                log.First().Message = message;
                log.First().ProcessStatus = processStatus;
                log.First().Status = status;
            }
            else
            {
                _logs.Insert(0, new Log { ProcessStatus = processStatus, Status = status, LogDate = logDate, JanCode = janCode, Message = message });
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == Column4.Name)
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
                else if (e.Value.ToString() == "Exception")
                {
                    DataGridViewCellStyle cellStyle = new DataGridViewCellStyle() { BackColor = System.Drawing.Color.Black, ForeColor = System.Drawing.Color.White };
                    dataGridView1.Rows[e.RowIndex].DefaultCellStyle = cellStyle;
                }
            }

            if (dataGridView1.Columns[e.ColumnIndex].Name == Column5.Name)
            {
                if (e.Value.ToString() == "End")
                {
                    e.Value = Properties.Resources.check_active;
                }
                else
                {
                    e.Value = Properties.Resources.check_deactive;
                }
                e.FormattingApplied = true;
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
