using NAudio.Wave;

namespace rans0m
{
    public partial class Ransomed : Form
    {
        // Really bad code, but I don't want to waste too much time on this, as long as it works 
        private int remainingTime = 3 * 26;
        public Point baseLocation;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private bool dragging = false;

        private Point basePicFace;
        private Point baseLblTitle;
        private Point basePnlMsg;
        private Point basePnlCash;
        private Point basePnlTimer;
        private System.Windows.Forms.Timer? internalJitterTimer;
        private bool isOpening = false;

        public static Ransomed? ActiveInstance { get; private set; }

        public Ransomed()
        {
            ActiveInstance = this;
            InitializeComponent();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (ActiveInstance == this) ActiveInstance = null;
        }

        public static Point LastKnownCashTargetCenter { get; private set; } = Point.Empty;

        public Point GetCashTargetCenter()
        {
            if (this.IsDisposed)
            {
                if (!LastKnownCashTargetCenter.IsEmpty) return LastKnownCashTargetCenter;
                int screenW = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
                int screenH = Screen.PrimaryScreen?.Bounds.Height ?? 1080;
                return new Point(screenW / 2, screenH / 2);
            }

            try
            {
                if (this.IsHandleCreated && pnl_cash != null && pnl_cash.IsHandleCreated)
                {
                    Point p = pnl_cash.PointToScreen(new Point(pnl_cash.Width / 2, pnl_cash.Height / 2));
                    LastKnownCashTargetCenter = p;
                    return p;
                }
            }
            catch { }

            // Reliable geometric fallback based on form's known location (pnl_cash is at (12, 260) with size (198, 58))
            try
            {
                Point fallback = new Point(this.Location.X + 111, this.Location.Y + 289);
                LastKnownCashTargetCenter = fallback;
                return fallback;
            }
            catch { }

            if (!LastKnownCashTargetCenter.IsEmpty) return LastKnownCashTargetCenter;
            int sW = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            int sH = Screen.PrimaryScreen?.Bounds.Height ?? 1080;
            return new Point(sW / 2, sH / 2);
        }

        private bool isCompletingPayment = false;

        private async void CompletePaymentAfterCoinsFinish()
        {
            if (isCompletingPayment) return;
            isCompletingPayment = true;

            // Wait for remaining cascading coins in flight to land into the box and count down
            int waited = 0;
            while (CoinFlightOverlay.Instance.HasActiveCoins && waited < 900)
            {
                await Task.Delay(15);
                waited += 15;
            }

            if (!Global.underRansom) return;
            Global.underRansom = false;

            // Seamlessly open ThankYou with zero audio cut off
            new ThankYou().Show();
            this.Close();
        }

        public void OnSmallCoinArrived(int amount)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(() => OnSmallCoinArrived(amount));
                }
                catch { }
                return;
            }

            Global.ransomLeft = Math.Max(0, Global.ransomLeft - amount);
            txt_cashToPay.Text = Global.ransomLeft.ToString();

            // Brief golden pulse on cash panel
            pnl_cash.BackColor = Color.FromArgb(70, 255, 215, 0);
            Task.Delay(90).ContinueWith(_ =>
            {
                try
                {
                    if (!this.IsDisposed)
                        this.Invoke(() => pnl_cash.BackColor = Color.FromArgb(14, 14, 16));
                }
                catch { }
            });

            // Check if whole ransom has been paid
            if (Global.ransomLeft <= 0 && Global.underRansom)
            {
                CompletePaymentAfterCoinsFinish();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DragDropFix.Allow(this.Handle);
        }

        private void Ransomed_Load(object sender, EventArgs e)
        {
            this.Opacity = 0.97;

            // Init the window
            lbl_time.Text = $"{remainingTime / 60:D2}:{remainingTime % 60:D2}";
            txt_cashToPay.Text = Global.ransomLeft.ToString();
            Global.RandomPosControl(this);
            baseLocation = this.Location;
            GetCashTargetCenter();

            // Store internal baseline positions for independent internal trembling
            basePicFace = pictureBox1.Location;
            baseLblTitle = label1.Location;
            basePnlMsg = pnl_message.Location;
            basePnlCash = pnl_cash.Location;
            basePnlTimer = pnl_timer.Location;

            // Instantly spawn 6 taunt windows in parallel so all windows appear immediately!
            for (int i = 0; i < 6; i++)
            {
                TauntWindow tauntWindow = new TauntWindow();
                tauntWindow.Show();
            }

            // Snappy opening animation on the main ransom frame in parallel without delaying other windows
            _ = PlayOpeningAnimationAsync();

            timer1.Start();

            internalJitterTimer = new System.Windows.Forms.Timer { Interval = 45 };
            internalJitterTimer.Tick += (s, ev) =>
            {
                if (this.IsDisposed) return;
                pictureBox1.Location = new Point(basePicFace.X + Global.rng.Next(-1, 2), basePicFace.Y + Global.rng.Next(-1, 2));
                label1.Location = new Point(baseLblTitle.X + Global.rng.Next(-1, 2), baseLblTitle.Y + Global.rng.Next(-1, 2));
                pnl_message.Location = new Point(basePnlMsg.X + Global.rng.Next(-1, 2), basePnlMsg.Y + Global.rng.Next(-1, 2));
                pnl_cash.Location = new Point(basePnlCash.X + Global.rng.Next(-1, 2), basePnlCash.Y + Global.rng.Next(-1, 2));
                pnl_timer.Location = new Point(basePnlTimer.X + Global.rng.Next(-1, 2), basePnlTimer.Y + Global.rng.Next(-1, 2));
            };
            internalJitterTimer.Start();

            new Thread(async () => Global.GlitchIdle(this, false)) { IsBackground = true }.Start();
        }

        private async Task PlayOpeningAnimationAsync()
        {
            isOpening = true;

            int targetW = this.Width;
            int targetH = this.Height;
            int cx = targetW / 2;
            int cy = targetH / 2;

            int baseX = baseLocation.X;
            int baseY = baseLocation.Y;

            // Phase 1: Rapid horizontal laser slit flash (12ms)
            int[] slitWidths = new int[] { 220, targetW };
            foreach (int sw in slitWidths)
            {
                try
                {
                    this.Region = new Region(new Rectangle(cx - sw / 2, cy - 3, sw, 6));
                    this.Refresh();
                }
                catch { }
                await Task.Delay(6);
            }

            // Phase 2: Instant elastic vertical burst (18ms)
            int[] heights = new int[] { 140, targetH + 10, targetH };
            for (int i = 0; i < heights.Length; i++)
            {
                int ch = Math.Clamp(heights[i], 6, targetH + 20);
                try
                {
                    this.Region = new Region(new Rectangle(0, cy - ch / 2, targetW, ch));
                    int jx = (i == 0) ? Global.rng.Next(-2, 3) : 0;
                    this.Location = new Point(baseX + jx, baseY);
                    this.Refresh();
                }
                catch { }
                await Task.Delay(6);
            }

            // Phase 3: Slam & Quick Impact Tremor (24ms)
            try
            {
                this.Region = null;
            }
            catch { }


            Point[] shudderOffsets = new Point[]
            {
                new Point(5, -3),
                new Point(-4, 2),
                new Point(0, 0)
            };

            foreach (var off in shudderOffsets)
            {
                try
                {
                    this.Location = new Point(baseX + off.X, baseY + off.Y);
                    this.Refresh();
                }
                catch { }
                await Task.Delay(8);
            }

            this.Location = baseLocation;
            isOpening = false;
            this.Refresh();
        }

        /// <summary>
        /// Penalizes the player by deducting seconds from the remaining time.
        /// Called when the user tries to close a taunt window.
        /// </summary>
        public void PenalizeTime(int seconds)
        {
            remainingTime = Math.Max(0, remainingTime - seconds);
            txt_cashToPay.Text = Global.ransomLeft.ToString();
            lbl_time.Text = $"{remainingTime / 60:D2}:{remainingTime % 60:D2}";

            // Immediately update the vignette to reflect the new urgency
            double progress = Math.Clamp(1.0 - (remainingTime / 78.0), 0.0, 1.0);
            Overlay.Instance?.UpdateVignetteProgress(progress);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Update remaining time, and the cash and time labels
            remainingTime--;
            txt_cashToPay.Text = Global.ransomLeft.ToString();
            lbl_time.Text = $"{remainingTime / 60:D2}:{remainingTime % 60:D2}";

            // Continuously escalate border reach and boiling speed as time runs down
            double progress = Math.Clamp(1.0 - (remainingTime / 78.0), 0.0, 1.0);
            Overlay.Instance?.UpdateVignetteProgress(progress);

            // Urgency visual escalation matching Doors
            if (remainingTime <= 30 && remainingTime > 10)
            {
                Color c = (remainingTime % 2 == 0) ? Color.FromArgb(25, 0, 0) : Color.FromArgb(255, 230, 0);
                lbl_time.ForeColor = c;
                lbl_timeTitle.ForeColor = c;
            }
            else if (remainingTime <= 10)
            {
                pnl_timer.BackColor = (remainingTime % 2 == 0) ? Color.Black : Color.FromArgb(235, 15, 15);
                Color c = (remainingTime % 2 == 0) ? Color.Red : Color.FromArgb(25, 0, 0);
                lbl_time.ForeColor = c;
                lbl_timeTitle.ForeColor = c;
            }

            // Keep on top of other windows
            if (!this.IsDisposed)
            {
                this.TopMost = true;
            }
        }

        private void pnl_message_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Draw crisp white border matching screenshot
            ControlPaint.DrawBorder(e.Graphics, pnl_message.ClientRectangle,
                Color.FromArgb(235, 235, 235), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(235, 235, 235), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(235, 235, 235), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(235, 235, 235), 2, ButtonBorderStyle.Solid);

            using (Font font = new Font("Arial", 9.8f, FontStyle.Bold))
            using (Brush whiteBrush = new SolidBrush(Color.White))
            using (Brush redBrush = new SolidBrush(Color.FromArgb(240, 20, 20)))
            {
                string line1 = "IF YOU DO NOT PAY THIS RANSOM";
                string line2 = "BEFORE THE TIMER ENDS, YOUR ITEMS";
                string line3Pre = "WILL BE ";
                string line3Red = "UNRECOVERABLE BY ANY";
                string line4Red = "MEANS.";

                SizeF s1 = e.Graphics.MeasureString(line1, font);
                float x1 = (pnl_message.Width - s1.Width) / 2f;
                e.Graphics.DrawString(line1, font, whiteBrush, x1, 5f);

                SizeF s2 = e.Graphics.MeasureString(line2, font);
                float x2 = (pnl_message.Width - s2.Width) / 2f;
                e.Graphics.DrawString(line2, font, whiteBrush, x2, 22f);

                SizeF sPre = e.Graphics.MeasureString(line3Pre, font);
                SizeF sRed3 = e.Graphics.MeasureString(line3Red, font);
                float totalW3 = sPre.Width + sRed3.Width - 8f;
                float x3 = (pnl_message.Width - totalW3) / 2f;
                float y3 = 39f;

                e.Graphics.DrawString(line3Pre, font, whiteBrush, x3, y3);
                e.Graphics.DrawString(line3Red, font, redBrush, x3 + sPre.Width - 6f, y3);

                SizeF s4 = e.Graphics.MeasureString(line4Red, font);
                float x4 = (pnl_message.Width - s4.Width) / 2f;
                e.Graphics.DrawString(line4Red, font, redBrush, x4, 56f);
            }
        }

        private void pnl_cash_Paint(object sender, PaintEventArgs e)
        {
            // Gold border matching screenshot
            ControlPaint.DrawBorder(e.Graphics, pnl_cash.ClientRectangle,
                Color.FromArgb(255, 215, 0), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(255, 215, 0), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(255, 215, 0), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(255, 215, 0), 2, ButtonBorderStyle.Solid);
        }

        private void pnl_timer_Paint(object sender, PaintEventArgs e)
        {
            // Crisp white border matching screenshot
            ControlPaint.DrawBorder(e.Graphics, pnl_timer.ClientRectangle,
                Color.White, 2, ButtonBorderStyle.Solid,
                Color.White, 2, ButtonBorderStyle.Solid,
                Color.White, 2, ButtonBorderStyle.Solid,
                Color.White, 2, ButtonBorderStyle.Solid);
        }

        private void pnl_titleBar_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                pnl_titleBar.ClientRectangle,
                Color.FromArgb(95, 95, 102),
                Color.FromArgb(32, 32, 36),
                System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, pnl_titleBar.ClientRectangle);
            }

            using (Pen topHighlight = new Pen(Color.FromArgb(170, 255, 255, 255), 1))
            using (Pen bottomLine = new Pen(Color.FromArgb(20, 20, 22), 1))
            {
                e.Graphics.DrawLine(topHighlight, 0, 0, pnl_titleBar.Width, 0);
                e.Graphics.DrawLine(bottomLine, 0, pnl_titleBar.Height - 1, pnl_titleBar.Width, pnl_titleBar.Height - 1);
            }
        }

        private void Ransomed_Paint(object sender, PaintEventArgs e)
        {
            // Crisp form border matching screenshot
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                Color.FromArgb(210, 210, 215), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(210, 210, 215), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(210, 210, 215), 2, ButtonBorderStyle.Solid,
                Color.FromArgb(210, 210, 215), 2, ButtonBorderStyle.Solid);

            // CRT scanlines during custom opening animation
            if (isOpening)
            {
                using (var scanPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
                {
                    for (int y = 0; y < ClientRectangle.Height; y += 4)
                    {
                        e.Graphics.DrawLine(scanPen, 0, y, ClientRectangle.Width, y);
                    }
                }
            }
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                baseLocation = new Point(dragFormPoint.X + diff.X, dragFormPoint.Y + diff.Y);
                this.Location = baseLocation;
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        // ------------ EVENT HANDLERS ------------------------------------------
        private void Ransomed_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // TODO : Check if file is a valid .gold file
                e.Effect = DragDropEffects.Link;
            }
        }

        private void Ransomed_DragDrop(object sender, DragEventArgs e)
        {
            string[]? files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (files == null) return;

            foreach (string file in files)
            {
                if (file.EndsWith(".gold", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".pot", StringComparison.OrdinalIgnoreCase))
                {
                    CoinFlightOverlay.CollectCoinFromDisk(file, Cursor.Position);
                }
            }
        }

    }
}
