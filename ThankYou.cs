using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace rans0m
{
    public partial class ThankYou : Form
    {
        private const int PROCESSING_DURATION_MS = 1000;
        private const int TOTAL_DURATION_MS = 3600;

        private readonly System.Windows.Forms.Timer animTimer;
        private readonly Stopwatch stopwatch;
        private readonly Random rng = new Random();

        private bool thankYouTriggered = false;

        private static readonly Bitmap cachedOkSign = Properties.Resources.ok_sign;

        // Corrupt red palette for the rotating wheel
        private static readonly Color[] WheelColors = new[]
        {
            Color.FromArgb(245, 30, 30),
            Color.FromArgb(210, 25, 25),
            Color.FromArgb(170, 20, 20),
            Color.FromArgb(130, 15, 15),
            Color.FromArgb(90, 10, 10),
            Color.FromArgb(60, 5, 5),
            Color.FromArgb(35, 0, 0),
            Color.FromArgb(15, 0, 0)
        };

        public ThankYou()
        {
            InitializeComponent();

            // Hide designer controls so custom painting renders seamlessly
            pc_oksign.Visible = false;
            txt_ty.Visible = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;

            stopwatch = new Stopwatch();

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 16; // ~60 FPS for jitter & animation
            animTimer.Tick += AnimTimer_Tick;
        }

        private void ThankYou_Load(object sender, EventArgs e)
        {
            Global.CenterControl(this);
            this.TopMost = true;

            // Seamless handoff: stop background OST cleanly and start processing sound with zero gap
            Global.StopMusic?.Invoke();
            CoinSoundSynthesizer.PlayProcessingSound(0.85f);

            stopwatch.Start();
            animTimer.Start();
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            long elapsed = stopwatch.ElapsedMilliseconds;

            if (elapsed >= TOTAL_DURATION_MS)
            {
                animTimer.Stop();
                Close();
                return;
            }

            if (elapsed >= PROCESSING_DURATION_MS && !thankYouTriggered)
            {
                thankYouTriggered = true;
                this.Text = "THANK YOU!";

                // Stop the processing sound cleanly
                CoinSoundSynthesizer.StopProcessingSound();

                // Small impact sound when processing finishes (same as download completion)
                CoinSoundSynthesizer.PlayWindowSlamGlitch(0.85f);

                try
                {
                    WaveOut cashSfx = SoundHelper.Create(Properties.Resources.cash);
                    cashSfx.Volume = 0.85f;
                    cashSfx.Play();
                }
                catch { }

                // Officially complete ransom now that payment has been processed and verified
                try { Global.RansomPayed?.Invoke(); } catch { }

                // Trigger Doors Firewall achievement badge notification
                BadgeNotificationForm.ShowBadge();
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            long elapsed = stopwatch.ElapsedMilliseconds;

            if (elapsed < PROCESSING_DURATION_MS)
            {
                DrawProcessingScreen(g, elapsed);
            }
            else
            {
                DrawThankYouScreen(g);
            }
        }

        private void DrawProcessingScreen(Graphics g, long elapsed)
        {
            int width = ClientSize.Width > 50 ? ClientSize.Width : 544;
            int height = ClientSize.Height > 50 ? ClientSize.Height : 315;

            // Pure black corrupt background
            g.Clear(Color.Black);

            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Glitch text jitter (exact same as downloadTextJitter in Overlay.cs)
            int jitterX = rng.Next(-2, 3);
            int jitterY = rng.Next(-2, 3);

            // 1. Text: "PROCESSING PAYMENT..." in bold white Arial (same font as txt_download)
            using (var font = new Font("Arial", 20F, FontStyle.Bold, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.White))
            {
                string text = "PROCESSING PAYMENT...";
                SizeF sz = g.MeasureString(text, font);
                float tx = (width - sz.Width) / 2f + jitterX;
                float ty = 36f + jitterY;
                g.DrawString(text, font, brush, tx, ty);
            }

            // 2. Simple corrupt style wheel rotating with NO easing (snaps every 75ms)
            int wheelStep = (int)(elapsed / 75) % 8;
            float cx = width / 2f + jitterX;
            float cy = 125f + jitterY;

            float rInner = 8f;
            float rOuter = 21f;

            for (int i = 0; i < 8; i++)
            {
                int colorIndex = (i - wheelStep + 8) % 8;
                Color spokeColor = WheelColors[colorIndex];

                using (var pen = new Pen(spokeColor, 4.0f))
                {
                    double angleRad = i * Math.PI / 4.0;
                    float x1 = cx + (float)(Math.Cos(angleRad) * rInner);
                    float y1 = cy + (float)(Math.Sin(angleRad) * rInner);
                    float x2 = cx + (float)(Math.Cos(angleRad) * rOuter);
                    float y2 = cy + (float)(Math.Sin(angleRad) * rOuter);

                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }

            // 3. Simple bar like the downloading one (DownloadBar style)
            int barW = 460;
            int barH = 26;
            int barX = (width - barW) / 2 + jitterX;
            int barY = 195 + jitterY;

            // Segment count & step-by-step fill with NO easing (0 to 10 segments)
            int segmentsCount = 10;
            int filledSegments = Math.Clamp((int)(elapsed / 90), 0, segmentsCount);

            int innerX = barX + 2;
            int innerY = barY + 2;
            int innerW = barW - 4;
            int innerH = barH - 4;
            float segW = (float)innerW / segmentsCount;

            // Black interior
            using (var bgBrush = new SolidBrush(Color.Black))
            {
                g.FillRectangle(bgBrush, barX, barY, barW, barH);
            }

            // Segment fills
            for (int i = 0; i < segmentsCount; i++)
            {
                float x = innerX + i * segW;
                float nextX = innerX + (i + 1) * segW;
                float segWidth = nextX - x;

                if (i < filledSegments)
                {
                    var rect = new RectangleF(x, innerY, segWidth, innerH);
                    using (var lgb = new LinearGradientBrush(rect, Color.White, Color.White, LinearGradientMode.Vertical))
                    {
                        var blend = new ColorBlend(3);
                        blend.Colors = new Color[]
                        {
                            Color.FromArgb(228, 35, 35),
                            Color.FromArgb(165, 20, 20),
                            Color.FromArgb(220, 30, 30)
                        };
                        blend.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                        lgb.InterpolationColors = blend;
                        g.FillRectangle(lgb, rect);
                    }
                }

                // Vertical divider between segments
                if (i > 0)
                {
                    using (var divPen = new Pen(Color.FromArgb(15, 0, 0), 2.0f))
                    {
                        g.DrawLine(divPen, x, innerY, x, innerY + innerH);
                    }
                }
            }

            // Outer 2px bright red border
            using (var borderPen = new Pen(Color.FromArgb(235, 22, 22), 2.0f))
            {
                g.DrawRectangle(borderPen, barX, barY, barW, barH);
            }
        }

        private void DrawThankYouScreen(Graphics g)
        {
            int width = ClientSize.Width > 50 ? ClientSize.Width : 544;
            int height = ClientSize.Height > 50 ? ClientSize.Height : 315;

            // Classic green background
            g.Clear(Color.FromArgb(0, 192, 0));

            // 1. "THANK YOU!" text in Consolas 48pt Bold Italic (original style)
            using (var font = new Font("Consolas", 48F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.White))
            {
                string text = "THANK YOU!";
                SizeF sz = g.MeasureString(text, font);
                float tx = (width - sz.Width) / 2f;
                float ty = 6f;
                g.DrawString(text, font, brush, tx, ty);
            }

            // 2. ok_sign image (210x210, centered, static - no easing)
            if (cachedOkSign != null)
            {
                int imgSize = 210;
                int imgX = (width - imgSize) / 2;
                int imgY = 88;

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(cachedOkSign, imgX, imgY, imgSize, imgSize);
            }
        }

        public Bitmap RenderFrame(long elapsedMs)
        {
            int width = ClientSize.Width > 50 ? ClientSize.Width : 544;
            int height = ClientSize.Height > 50 ? ClientSize.Height : 315;
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                if (elapsedMs < PROCESSING_DURATION_MS)
                {
                    DrawProcessingScreen(g, elapsedMs);
                }
                else
                {
                    DrawThankYouScreen(g);
                }
            }
            return bmp;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CoinSoundSynthesizer.StopProcessingSound();
            try { Global.RansomPayed?.Invoke(); } catch { }
            animTimer.Stop();
            animTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
