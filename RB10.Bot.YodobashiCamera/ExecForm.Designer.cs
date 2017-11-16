namespace RB10.Bot.YodobashiCamera
{
    partial class ExecForm
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.Column5 = new System.Windows.Forms.DataGridViewImageColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.FileSelectButton = new System.Windows.Forms.Button();
            this.JanCodeFileTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.RunButton = new System.Windows.Forms.Button();
            this.IncludeUnPostedCheckBox = new System.Windows.Forms.CheckBox();
            this.DelayNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.RunAsyncButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DelayNumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column5,
            this.Column4,
            this.Column2,
            this.Column1,
            this.Column3});
            this.dataGridView1.Location = new System.Drawing.Point(2, 130);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.RowTemplate.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            this.dataGridView1.RowTemplate.Height = 21;
            this.dataGridView1.Size = new System.Drawing.Size(946, 472);
            this.dataGridView1.TabIndex = 26;
            // 
            // Column5
            // 
            this.Column5.DataPropertyName = "ProcessStatus";
            this.Column5.HeaderText = "";
            this.Column5.ImageLayout = System.Windows.Forms.DataGridViewImageCellLayout.Stretch;
            this.Column5.Name = "Column5";
            this.Column5.ReadOnly = true;
            this.Column5.Width = 21;
            // 
            // Column4
            // 
            this.Column4.DataPropertyName = "Status";
            this.Column4.HeaderText = "ステータス";
            this.Column4.Name = "Column4";
            this.Column4.ReadOnly = true;
            this.Column4.Width = 150;
            // 
            // Column2
            // 
            this.Column2.DataPropertyName = "LogDate";
            this.Column2.HeaderText = "日時";
            this.Column2.Name = "Column2";
            this.Column2.ReadOnly = true;
            this.Column2.Width = 150;
            // 
            // Column1
            // 
            this.Column1.DataPropertyName = "JanCode";
            this.Column1.HeaderText = "JANコード";
            this.Column1.Name = "Column1";
            this.Column1.ReadOnly = true;
            // 
            // Column3
            // 
            this.Column3.DataPropertyName = "Message";
            this.Column3.HeaderText = "メッセージ";
            this.Column3.Name = "Column3";
            this.Column3.ReadOnly = true;
            this.Column3.Width = 480;
            // 
            // FileSelectButton
            // 
            this.FileSelectButton.Location = new System.Drawing.Point(911, 18);
            this.FileSelectButton.Name = "FileSelectButton";
            this.FileSelectButton.Size = new System.Drawing.Size(29, 26);
            this.FileSelectButton.TabIndex = 25;
            this.FileSelectButton.Text = "...";
            this.FileSelectButton.UseVisualStyleBackColor = true;
            this.FileSelectButton.Click += new System.EventHandler(this.FileSelectButton_Click);
            // 
            // JanCodeFileTextBox
            // 
            this.JanCodeFileTextBox.Location = new System.Drawing.Point(156, 19);
            this.JanCodeFileTextBox.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.JanCodeFileTextBox.Name = "JanCodeFileTextBox";
            this.JanCodeFileTextBox.Size = new System.Drawing.Size(749, 25);
            this.JanCodeFileTextBox.TabIndex = 24;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(138, 18);
            this.label1.TabIndex = 23;
            this.label1.Text = "JANコードCSVファイル";
            // 
            // RunButton
            // 
            this.RunButton.Location = new System.Drawing.Point(815, 77);
            this.RunButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.RunButton.Name = "RunButton";
            this.RunButton.Size = new System.Drawing.Size(125, 34);
            this.RunButton.TabIndex = 22;
            this.RunButton.Text = "実行";
            this.RunButton.UseVisualStyleBackColor = true;
            this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
            // 
            // IncludeUnPostedCheckBox
            // 
            this.IncludeUnPostedCheckBox.AutoSize = true;
            this.IncludeUnPostedCheckBox.Location = new System.Drawing.Point(593, 84);
            this.IncludeUnPostedCheckBox.Name = "IncludeUnPostedCheckBox";
            this.IncludeUnPostedCheckBox.Size = new System.Drawing.Size(219, 22);
            this.IncludeUnPostedCheckBox.TabIndex = 31;
            this.IncludeUnPostedCheckBox.Text = "未掲載だった商品もサイト検索する";
            this.IncludeUnPostedCheckBox.UseVisualStyleBackColor = true;
            // 
            // DelayNumericUpDown
            // 
            this.DelayNumericUpDown.Location = new System.Drawing.Point(656, 58);
            this.DelayNumericUpDown.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.DelayNumericUpDown.Name = "DelayNumericUpDown";
            this.DelayNumericUpDown.Size = new System.Drawing.Size(72, 25);
            this.DelayNumericUpDown.TabIndex = 28;
            this.DelayNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.DelayNumericUpDown.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(728, 61);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(80, 18);
            this.label3.TabIndex = 30;
            this.label3.Text = "ミリ秒にする";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(590, 61);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(68, 18);
            this.label2.TabIndex = 29;
            this.label2.Text = "取得間隔を";
            // 
            // RunAsyncButton
            // 
            this.RunAsyncButton.Location = new System.Drawing.Point(12, 52);
            this.RunAsyncButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.RunAsyncButton.Name = "RunAsyncButton";
            this.RunAsyncButton.Size = new System.Drawing.Size(125, 34);
            this.RunAsyncButton.TabIndex = 27;
            this.RunAsyncButton.Text = "非同期実行";
            this.RunAsyncButton.UseVisualStyleBackColor = true;
            this.RunAsyncButton.Visible = false;
            // 
            // ExecForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(951, 605);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.FileSelectButton);
            this.Controls.Add(this.JanCodeFileTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.RunButton);
            this.Controls.Add(this.IncludeUnPostedCheckBox);
            this.Controls.Add(this.DelayNumericUpDown);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.RunAsyncButton);
            this.Font = new System.Drawing.Font("メイリオ", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "ExecForm";
            this.Text = "ヨドバシカメラ スクレイピング";
            this.Load += new System.EventHandler(this.ExecForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DelayNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewImageColumn Column5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.Button FileSelectButton;
        private System.Windows.Forms.TextBox JanCodeFileTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button RunButton;
        private System.Windows.Forms.CheckBox IncludeUnPostedCheckBox;
        private System.Windows.Forms.NumericUpDown DelayNumericUpDown;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button RunAsyncButton;
    }
}

