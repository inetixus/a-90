namespace rans0m
{
    partial class Ransomed
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            pnl_titleBar = new Panel();
            lbl_windowTitle = new Label();
            pictureBox1 = new PictureBox();
            label1 = new Label();
            pnl_message = new Panel();
            pnl_cash = new Panel();
            txt_cashToPay = new Label();
            pictureBox2 = new PictureBox();
            pnl_timer = new Panel();
            lbl_timeTitle = new Label();
            lbl_time = new Label();
            btn_quickPay = new Button();
            timer1 = new System.Windows.Forms.Timer(components);
            pnl_titleBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            pnl_cash.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            pnl_timer.SuspendLayout();
            SuspendLayout();
            // 
            // pnl_titleBar
            // 
            pnl_titleBar.BackColor = Color.FromArgb(48, 48, 52);
            pnl_titleBar.Controls.Add(lbl_windowTitle);
            pnl_titleBar.Dock = DockStyle.Top;
            pnl_titleBar.Location = new Point(0, 0);
            pnl_titleBar.Name = "pnl_titleBar";
            pnl_titleBar.Size = new Size(544, 28);
            pnl_titleBar.TabIndex = 6;
            pnl_titleBar.Paint += pnl_titleBar_Paint;
            pnl_titleBar.MouseDown += TitleBar_MouseDown;
            pnl_titleBar.MouseMove += TitleBar_MouseMove;
            pnl_titleBar.MouseUp += TitleBar_MouseUp;
            // 
            // lbl_windowTitle
            // 
            lbl_windowTitle.AutoSize = true;
            lbl_windowTitle.BackColor = Color.Transparent;
            lbl_windowTitle.Font = new Font("Arial", 9.5F, FontStyle.Bold);
            lbl_windowTitle.ForeColor = Color.FromArgb(220, 220, 220);
            lbl_windowTitle.Location = new Point(10, 6);
            lbl_windowTitle.Name = "lbl_windowTitle";
            lbl_windowTitle.Size = new Size(68, 16);
            lbl_windowTitle.TabIndex = 0;
            lbl_windowTitle.Text = "RANSOM";
            lbl_windowTitle.MouseDown += TitleBar_MouseDown;
            lbl_windowTitle.MouseMove += TitleBar_MouseMove;
            lbl_windowTitle.MouseUp += TitleBar_MouseUp;
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = Color.Transparent;
            pictureBox1.Image = Properties.Resources.ransom_idle;
            pictureBox1.Location = new Point(16, 38);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(130, 130);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // label1
            // 
            label1.BackColor = Color.Transparent;
            label1.Font = new Font("Arial", 22F, FontStyle.Bold);
            label1.ForeColor = Color.White;
            label1.Location = new Point(155, 36);
            label1.Name = "label1";
            label1.Size = new Size(375, 130);
            label1.TabIndex = 1;
            label1.Text = "YOUR ITEMS\nHAVE BEEN\nENCRYPTED";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pnl_message
            // 
            pnl_message.BackColor = Color.Black;
            pnl_message.Location = new Point(12, 172);
            pnl_message.Name = "pnl_message";
            pnl_message.Size = new Size(520, 80);
            pnl_message.TabIndex = 2;
            pnl_message.Paint += pnl_message_Paint;
            // 
            // pnl_cash
            // 
            pnl_cash.BackColor = Color.Black;
            pnl_cash.Controls.Add(txt_cashToPay);
            pnl_cash.Controls.Add(pictureBox2);
            pnl_cash.Location = new Point(12, 260);
            pnl_cash.Name = "pnl_cash";
            pnl_cash.Size = new Size(198, 58);
            pnl_cash.TabIndex = 3;
            pnl_cash.Paint += pnl_cash_Paint;
            // 
            // txt_cashToPay
            // 
            txt_cashToPay.BackColor = Color.Transparent;
            txt_cashToPay.Font = new Font("Arial", 26F, FontStyle.Bold);
            txt_cashToPay.ForeColor = Color.FromArgb(255, 225, 30);
            txt_cashToPay.Location = new Point(6, 4);
            txt_cashToPay.Name = "txt_cashToPay";
            txt_cashToPay.Size = new Size(126, 50);
            txt_cashToPay.TabIndex = 0;
            txt_cashToPay.Text = "500";
            txt_cashToPay.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pictureBox2
            // 
            pictureBox2.BackColor = Color.Transparent;
            pictureBox2.Image = Properties.Resources.Gold;
            pictureBox2.Location = new Point(136, 6);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(54, 46);
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox2.TabIndex = 1;
            pictureBox2.TabStop = false;
            // 
            // pnl_timer
            // 
            pnl_timer.BackColor = Color.FromArgb(235, 15, 15);
            pnl_timer.Controls.Add(lbl_timeTitle);
            pnl_timer.Controls.Add(lbl_time);
            pnl_timer.Location = new Point(220, 260);
            pnl_timer.Name = "pnl_timer";
            pnl_timer.Size = new Size(312, 58);
            pnl_timer.TabIndex = 4;
            pnl_timer.Paint += pnl_timer_Paint;
            // 
            // lbl_timeTitle
            // 
            lbl_timeTitle.BackColor = Color.Transparent;
            lbl_timeTitle.Font = new Font("Arial", 23F, FontStyle.Bold);
            lbl_timeTitle.ForeColor = Color.FromArgb(25, 0, 0);
            lbl_timeTitle.Location = new Point(10, 4);
            lbl_timeTitle.Name = "lbl_timeTitle";
            lbl_timeTitle.Size = new Size(110, 50);
            lbl_timeTitle.TabIndex = 1;
            lbl_timeTitle.Text = "TIME:";
            lbl_timeTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lbl_time
            // 
            lbl_time.BackColor = Color.Transparent;
            lbl_time.Font = new Font("Arial", 23F, FontStyle.Bold);
            lbl_time.ForeColor = Color.FromArgb(25, 0, 0);
            lbl_time.Location = new Point(135, 4);
            lbl_time.Name = "lbl_time";
            lbl_time.Size = new Size(165, 50);
            lbl_time.TabIndex = 0;
            lbl_time.Text = "01:18";
            lbl_time.TextAlign = ContentAlignment.MiddleRight;
            // 
            // btn_quickPay
            // 
            btn_quickPay.BackColor = Color.FromArgb(28, 28, 28);
            btn_quickPay.Cursor = Cursors.Hand;
            btn_quickPay.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btn_quickPay.FlatStyle = FlatStyle.Flat;
            btn_quickPay.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btn_quickPay.ForeColor = Color.FromArgb(255, 215, 0);
            btn_quickPay.Location = new Point(12, 328);
            btn_quickPay.Name = "btn_quickPay";
            btn_quickPay.Size = new Size(520, 32);
            btn_quickPay.TabIndex = 5;
            btn_quickPay.Text = "[DEV TESTING] Quick Pay All Coins";
            btn_quickPay.UseVisualStyleBackColor = false;
            btn_quickPay.Click += btn_quickPay_Click;
            // 
            // timer1
            // 
            timer1.Interval = 1000;
            timer1.Tick += timer1_Tick;
            // 
            // Ransomed
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(220, 0, 0);
            ClientSize = new Size(544, 372);
            ControlBox = false;
            Controls.Add(btn_quickPay);
            Controls.Add(pnl_timer);
            Controls.Add(pnl_cash);
            Controls.Add(pnl_message);
            Controls.Add(label1);
            Controls.Add(pictureBox1);
            Controls.Add(pnl_titleBar);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MaximumSize = new Size(544, 372);
            MdiChildrenMinimizedAnchorBottom = false;
            MinimizeBox = false;
            MinimumSize = new Size(544, 372);
            Name = "Ransomed";
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.Manual;
            Text = "RANS0M";
            TopMost = true;
            Load += Ransomed_Load;
            Paint += Ransomed_Paint;
            DragDrop += Ransomed_DragDrop;
            DragEnter += Ransomed_DragEnter;
            pnl_titleBar.ResumeLayout(false);
            pnl_titleBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            pnl_cash.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            pnl_timer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel pnl_titleBar;
        private Label lbl_windowTitle;
        private PictureBox pictureBox1;
        private Label label1;
        private Panel pnl_message;
        private Panel pnl_cash;
        private Label txt_cashToPay;
        private PictureBox pictureBox2;
        private Panel pnl_timer;
        private Label lbl_timeTitle;
        private Label lbl_time;
        private Button btn_quickPay;
        private System.Windows.Forms.Timer timer1;
    }
}