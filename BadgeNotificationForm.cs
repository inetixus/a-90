using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rans0m
{
    public class BadgeNotificationForm : Form
    {
        private static readonly Bitmap cachedBadge = Properties.Resources.badge;
        private readonly int badgeW = 454;
        private readonly int badgeH = 96;
        private readonly int hiddenX;
        private readonly int shownX;
        private readonly int posY;

        public BadgeNotificationForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;

            // Transparent backing so only the rounded badge is visible
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;

            Size = new Size(badgeW, badgeH);

            Rectangle screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            hiddenX = screen.Right;
            // Leave ~28px behind the screen on the right so the right closure is hidden
            shownX = screen.Right - (badgeW - 28);
            // Position around top right corner, but a little lower
            posY = screen.Top + 70;

            Location = new Point(hiddenX, posY);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (cachedBadge != null)
            {
                g.DrawImage(cachedBadge, new Rectangle(0, 0, badgeW, badgeH));
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = RunAnimationAsync();
        }

        private async Task RunAnimationAsync()
        {
            // Play achievement sound the moment it starts appearing
            CoinSoundSynthesizer.PlayAchievementSound(0.92f);

            var sw = Stopwatch.StartNew();

            // 1. Slide In (Tween towards outside / on-screen) - 620ms Fast-Start Smooth Ease-Out
            double slideInDuration = 620.0;
            while (sw.ElapsedMilliseconds < slideInDuration)
            {
                double progress = Math.Clamp(sw.ElapsedMilliseconds / slideInDuration, 0.0, 1.0);
                double ease = 1.0 - Math.Pow(1.0 - progress, 3.2);
                int curX = (int)(hiddenX + (shownX - hiddenX) * ease);

                try
                {
                    this.Location = new Point(curX, posY);
                }
                catch { break; }

                await Task.Delay(8);
            }

            this.Location = new Point(shownX, posY);

            // 2. Hold on screen for 4.0 seconds
            await Task.Delay(4000);

            // 3. Slide Out (Tween back inside / off-screen) - 700ms Smooth Ease-In
            sw.Restart();
            double slideOutDuration = 700.0;
            while (sw.ElapsedMilliseconds < slideOutDuration)
            {
                double progress = Math.Clamp(sw.ElapsedMilliseconds / slideOutDuration, 0.0, 1.0);
                double ease = Math.Pow(progress, 2.8);
                int curX = (int)(shownX + (hiddenX - shownX) * ease);

                try
                {
                    this.Location = new Point(curX, posY);
                }
                catch { break; }

                await Task.Delay(8);
            }

            this.Location = new Point(hiddenX, posY);

            // 4. Close & clean up
            try
            {
                this.Close();
                this.Dispose();
                Application.ExitThread();
            }
            catch { }
        }

        /// <summary>
        /// Spawns the Doors Firewall badge animation on a dedicated STA thread.
        /// </summary>
        public static void ShowBadge()
        {
            try
            {
                var thread = new Thread(() =>
                {
                    var form = new BadgeNotificationForm();
                    Application.Run(form);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
            catch { }
        }
    }
}
