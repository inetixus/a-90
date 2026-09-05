using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rans0m
{
    public partial class TauntWindow : Form
    {
        private enum WindowStyle { ImageGlitch, SystemAlert, HazardBanner }
        private WindowStyle style;

        private Color headerBgColor;
        private Color headerTextColor;
        private Color borderColor;
        private string alertMessage = "";
        private Rectangle closeButtonRect;
        private Rectangle closeHitRect;

        public TauntWindow()
        {
            InitializeComponent();
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void TauntWindow_Load(object sender, EventArgs e)
        {
            lock (Global.activeTauntWindows)
            {
                Global.activeTauntWindows.Add(this);
            }

            // Pick random title
            Text = Global.tauntTitles[Global.rng.Next(Global.tauntTitles.Count)];

            // Randomize styling theme
            int theme = Global.rng.Next(5);
            switch (theme)
            {
                case 0: // Deep Blood Crimson
                    headerBgColor = Color.FromArgb(150, 10, 10);
                    headerTextColor = Color.FromArgb(255, 230, 230);
                    borderColor = Color.FromArgb(210, 20, 20);
                    break;
                case 1: // Hazard Caution Yellow
                    headerBgColor = Color.FromArgb(30, 30, 30);
                    headerTextColor = Color.FromArgb(255, 220, 0);
                    borderColor = Color.FromArgb(255, 200, 0);
                    break;
                case 2: // Retro Glitch Cyan
                    headerBgColor = Color.FromArgb(10, 30, 40);
                    headerTextColor = Color.FromArgb(40, 240, 255);
                    borderColor = Color.FromArgb(0, 200, 240);
                    break;
                case 3: // Inverted High-Contrast
                    headerBgColor = Color.FromArgb(220, 20, 20);
                    headerTextColor = Color.Black;
                    borderColor = Color.FromArgb(255, 60, 60);
                    break;
                default: // Terminal Phosphor Green
                    headerBgColor = Color.FromArgb(12, 25, 12);
                    headerTextColor = Color.FromArgb(50, 255, 80);
                    borderColor = Color.FromArgb(40, 200, 60);
                    break;
            }

            // Decide window type
            int styleRoll = Global.rng.Next(10);
            if (styleRoll < 6)
            {
                // Classic Image Glitch
                style = WindowStyle.ImageGlitch;
                BackgroundImage = Global.tauntImages[Global.rng.Next(Global.tauntImages.Count)];
                BackgroundImageLayout = ImageLayout.Stretch;

                int[] widths = new int[] { 220, 260, 300, 340, 250, 380, 230 };
                int[] heights = new int[] { 180, 220, 260, 200, 280, 230, 300 };
                int pick = Global.rng.Next(widths.Length);
                Size = new Size(widths[pick], heights[pick]);
            }
            else if (styleRoll < 9)
            {
                // Retro Malware System Warning Box
                style = WindowStyle.SystemAlert;
                BackColor = Color.FromArgb(14, 14, 16);
                BackgroundImage = null;
                alertMessage = Global.tauntMessages[Global.rng.Next(Global.tauntMessages.Count)];

                int w = Global.rng.Next(330, 420);
                int h = Global.rng.Next(170, 220);
                Size = new Size(w, h);
            }
            else
            {
                // Hazard Banner
                style = WindowStyle.HazardBanner;
                BackColor = Color.FromArgb(20, 2, 2);
                BackgroundImage = null;
                alertMessage = "CRITICAL A-90 THREAT DETECTED";

                int w = Global.rng.Next(380, 460);
                int h = Global.rng.Next(130, 160);
                Size = new Size(w, h);
            }

            MaximumSize = Size;
            MinimumSize = Size;
            // Visual close button position (small, for drawing the subtle X)
            closeButtonRect = new Rectangle(Width - 22, 2, 18, 18);
            // Hit area for close button detection (wider, covers right portion of header)
            closeHitRect = new Rectangle(Width - 36, 0, 36, 22);

            // Random initial position on screen
            Global.RandomPosControl(this);

            // Subtle Glitch Idle Tremble Effect
            new Thread(async () => Global.GlitchIdle(this)) { IsBackground = true }.Start();

            // Closes after 7 to 12 seconds to ensure high, dynamic density
            new Thread(async () =>
            {
                await Task.Delay(Global.rng.Next(7000, 12000));
                if (IsDisposed) return;
                try { this.Invoke(() => this.Close()); } catch { }
            }) { IsBackground = true }.Start();
        }

        private void TauntWindow_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1. Top retro alert header bar
            Rectangle headerRect = new Rectangle(0, 0, Width, 22);
            using (Brush headerBrush = new SolidBrush(headerBgColor))
            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(headerTextColor))
            {
                g.FillRectangle(headerBrush, headerRect);
                g.DrawString(Text, font, textBrush, 6, 3);

                // Draw nearly hidden close button [X] - subtle, blends with header
                int xAlpha = 35; // Very faint
                using (Font xFont = new Font("Arial", 8f, FontStyle.Regular))
                using (Brush xBrush = new SolidBrush(Color.FromArgb(xAlpha, headerTextColor)))
                {
                    g.DrawString("X", xFont, xBrush, closeButtonRect.X + 4, closeButtonRect.Y + 2);
                }
            }

            // 2. Custom content rendering for non-image styles
            if (style == WindowStyle.SystemAlert)
            {
                // Draw warning stop sign icon
                g.DrawImage(Properties.Resources.stop_sign, 14, 34, 48, 48);

                // Draw message text
                using (Font msgFont = new Font("Consolas", 8.5f, FontStyle.Regular))
                using (Brush msgBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                {
                    Rectangle textBounds = new Rectangle(70, 30, Width - 80, Height - 65);
                    g.DrawString(alertMessage, msgFont, msgBrush, textBounds);
                }

                // Draw fake dialog buttons [PAY NOW] [ABORT]
                int btnW = 85;
                int btnH = 22;
                Rectangle btnPay = new Rectangle(Width - btnW - 12, Height - btnH - 10, btnW, btnH);
                using (Brush btnBrush = new SolidBrush(Color.FromArgb(160, 20, 20)))
                using (Pen btnPen = new Pen(Color.FromArgb(240, 40, 40), 1))
                using (Font btnFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (Brush btnTxt = new SolidBrush(Color.White))
                {
                    g.FillRectangle(btnBrush, btnPay);
                    g.DrawRectangle(btnPen, btnPay);
                    g.DrawString("PAY 500", btnFont, btnTxt, btnPay.X + 16, btnPay.Y + 4);
                }
            }
            else if (style == WindowStyle.HazardBanner)
            {
                // Diagonal hazard stripes on bottom edge
                for (int x = -20; x < Width + 20; x += 24)
                {
                    using (Brush stripeBrush = new SolidBrush(Color.FromArgb(180, 150, 0)))
                    {
                        Point[] pts = new Point[]
                        {
                            new Point(x, Height - 14),
                            new Point(x + 12, Height - 14),
                            new Point(x + 2, Height),
                            new Point(x - 10, Height)
                        };
                        g.FillPolygon(stripeBrush, pts);
                    }
                }

                g.DrawImage(Properties.Resources.ransom_idle, 16, 32, 54, 54);
                using (Font bannerFont = new Font("Arial", 11f, FontStyle.Bold))
                using (Brush bannerBrush = new SolidBrush(Color.FromArgb(255, 40, 40)))
                {
                    g.DrawString("SECURITY COMPROMISED", bannerFont, bannerBrush, 80, 34);
                }
                using (Font subFont = new Font("Consolas", 9f, FontStyle.Regular))
                using (Brush subBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
                {
                    g.DrawString("A-90 HAS OVERRIDDEN THE SYSTEM\nDEPOSIT 500 COINS IMMEDIATELY", subFont, subBrush, 80, 56);
                }
            }

            // 3. Crisp outer border
            ControlPaint.DrawBorder(g, ClientRectangle,
                borderColor, 2, ButtonBorderStyle.Solid,
                borderColor, 2, ButtonBorderStyle.Solid,
                borderColor, 2, ButtonBorderStyle.Solid,
                borderColor, 2, ButtonBorderStyle.Solid);
        }

        // Playful malware hydra interaction: clicking spawns a twin or jumps!
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (Global.underRansom)
            {
                // Check if user clicked the fake [X] close button
                if (closeButtonRect.Contains(e.Location))
                {
                    // Punish the user for trying to close a window!
                    CoinSoundSynthesizer.PlayDamageSound(0.85f);

                    // Penalize the timer by 6-10 seconds
                    int penalty = Global.rng.Next(6, 11);
                    try
                    {
                        Ransomed.ActiveInstance?.Invoke(() =>
                        {
                            Ransomed.ActiveInstance?.PenalizeTime(penalty);
                        });
                    }
                    catch { }

                    // Teleport to a new random position to be annoying
                    Global.RandomPosControl(this);
                    return;
                }

                // Teleport window slightly and occasionally spawn a twin
                Location = new Point(
                    Math.Clamp(Location.X + Global.rng.Next(-40, 41), 0, Math.Max(10, Global.screenBounds.Width - Width)),
                    Math.Clamp(Location.Y + Global.rng.Next(-40, 41), 0, Math.Max(10, Global.screenBounds.Height - Height))
                );

                if (Global.rng.Next(10) < 4)
                {
                    lock (Global.activeTauntWindows)
                    {
                        Global.activeTauntWindows.RemoveAll(f => f == null || f.IsDisposed);
                        if (Global.activeTauntWindows.Count < 3)
                        {
                            TauntWindow twin = new TauntWindow();
                            twin.Show();
                        }
                    }
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            lock (Global.activeTauntWindows)
            {
                Global.activeTauntWindows.Remove(this);
            }
        }

        private void TauntWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            lock (Global.activeTauntWindows)
            {
                Global.activeTauntWindows.Remove(this);
            }
        }
    }
}
