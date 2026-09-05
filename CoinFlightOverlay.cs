using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace rans0m
{
    public class CoinFlightOverlay : Form
    {
        private static CoinFlightOverlay? instance;
        public static CoinFlightOverlay Instance => instance ??= new CoinFlightOverlay();

        private static readonly Bitmap cachedSmallCoin = new Bitmap(Properties.Resources.Gold, new Size(28, 28));

        private readonly System.Windows.Forms.Timer animationTimer;
        private readonly Stopwatch stopwatch = new();
        private readonly List<FlyingCoin> activeCoins = new();
        private readonly List<SparkleParticle> activeSparkles = new();
        private readonly object stateLock = new();

        private static class NativeMethods
        {
            public const int WS_EX_TRANSPARENT = 0x00000020;
            public const int WS_EX_NOACTIVATE = 0x08000000;
            public const int WS_EX_TOPMOST = 0x00000008;

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
            public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOACTIVATE = 0x0010;

            [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
            public static extern uint TimeBeginPeriod(uint uMilliseconds);

            [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
            public static extern uint TimeEndPeriod(uint uMilliseconds);
        }

        public CoinFlightOverlay()
        {
            instance = this;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            ShowInTaskbar = false;
            TopMost = true;

            // Pure chroma key transparency matching Overlay (zero gray box or fringing)
            BackColor = Color.FromArgb(0, 0, 192);
            TransparencyKey = Color.FromArgb(0, 0, 192);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // High-frequency animation timer
            animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
            animationTimer.Tick += OnAnimationTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
                return cp;
            }
        }

        public bool HasActiveCoins
        {
            get
            {
                lock (stateLock)
                {
                    return activeCoins.Count > 0;
                }
            }
        }

        private static Point GetCurrentRansomTarget()
        {
            if (Ransomed.ActiveInstance != null && !Ransomed.ActiveInstance.IsDisposed)
            {
                return Ransomed.ActiveInstance.GetCashTargetCenter();
            }

            if (!Ransomed.LastKnownCashTargetCenter.IsEmpty)
            {
                return Ransomed.LastKnownCashTargetCenter;
            }

            int screenW = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            int screenH = Screen.PrimaryScreen?.Bounds.Height ?? 1080;
            return new Point(screenW / 2, screenH / 2);
        }

        /// <summary>
        /// Validates a .gold or .pot file from disk and triggers the Doors coin flight animation
        /// </summary>
        public static void CollectCoinFromDisk(string filePath, Point? clickPosition = null)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            // Validate and mark item used
            var result = GoldCoinManager.TryCollectCoin(filePath);
            if (!result.Success)
                return;

            Point origin = clickPosition ?? Cursor.Position;
            if (result.IsHoneyPot)
            {
                Instance.LaunchHoneyPotCollection(origin, result.Value);
            }
            else
            {
                Instance.LaunchDoorsCoinCollection(origin, result.Value);
            }
        }

        /// <summary>
        /// Launches the Doors coin flight: 5 coins starting slow from near the coin,
        /// accelerating fast along Bezier curves into the ransom cash box with 60+ FPS smoothness.
        /// </summary>
        public void LaunchDoorsCoinCollection(Point origin, int totalValue = 25)
        {
            if (this.IsDisposed) return;

            if (!this.Visible)
            {
                this.Bounds = SystemInformation.VirtualScreen;
                this.Show();
            }

            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            // Enable 1ms Windows timer resolution for buttery 60+ FPS
            NativeMethods.TimeBeginPeriod(1);

            if (!stopwatch.IsRunning)
                stopwatch.Restart();
            else
                stopwatch.Start();

            double curTime = stopwatch.Elapsed.TotalMilliseconds;

            lock (stateLock)
            {
                Point target = GetCurrentRansomTarget();

                int coinCount = 5;
                int baseVal = totalValue / coinCount;
                int rem = totalValue % coinCount;

                for (int i = 0; i < coinCount; i++)
                {
                    int val = baseVal + (i == coinCount - 1 ? rem : 0);

                    // Each coin starts at a slightly randomized point close to the coin
                    int rx = Global.rng.Next(-22, 23);
                    int ry = Global.rng.Next(-18, 19);
                    Point coinStart = new Point(origin.X + rx, origin.Y + ry);

                    // Starts slow, accelerates fast: 360ms flight duration gives 25+ smooth frames per coin
                    double delay = i * 38.0;
                    double duration = 360.0;

                    // Compute curved Bezier control point (curves upward and outward)
                    float spreadX = (i - 2) * 28f + Global.rng.Next(-12, 13);
                    float midX = (coinStart.X + target.X) / 2f + spreadX;
                    float midY = Math.Min(coinStart.Y, target.Y) - Global.rng.Next(85, 145);

                    activeCoins.Add(new FlyingCoin
                    {
                        Origin = coinStart,
                        BurstTarget = coinStart,
                        ControlPoint = new PointF(midX, midY),
                        Target = target,
                        StartTimeMs = curTime,
                        BurstDurationMs = 0,
                        HoverDurationMs = 0,
                        FlightDelayMs = delay,
                        FlightDurationMs = duration,
                        PitchIndex = i,
                        Value = val,
                        CurrentPos = coinStart,
                        LastDrawnRect = new Rectangle(coinStart.X - this.Left - 16, coinStart.Y - this.Top - 16, 32, 32)
                    });
                }
            }

            if (!animationTimer.Enabled)
                animationTimer.Start();
        }

        /// <summary>
        /// Shatters the honey pot: plays authentic earthenware ceramic shatter sound,
        /// explodes clay pot shards & golden shockwave outward, pops all 20 coins radially outward
        /// around the pot location to hover briefly, and then streams them into the ransom window!
        /// </summary>
        public void LaunchHoneyPotCollection(Point origin, int totalValue = 250)
        {
            if (this.IsDisposed) return;

            // Play authentic earthenware ceramic shatter SFX immediately!
            CoinSoundSynthesizer.PlayVaseBreak(0.95f);

            if (!this.Visible)
            {
                this.Bounds = SystemInformation.VirtualScreen;
                this.Show();
            }

            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            NativeMethods.TimeBeginPeriod(1);

            if (!stopwatch.IsRunning)
                stopwatch.Restart();
            else
                stopwatch.Start();

            double curTime = stopwatch.Elapsed.TotalMilliseconds;

            lock (stateLock)
            {
                Point target = GetCurrentRansomTarget();

                // 20 Coins popping out around the pot, hovering, then streaming into the ransom window!
                int coinCount = 20;
                int baseVal = totalValue / coinCount;
                int rem = totalValue % coinCount;

                double burstDuration = 220.0; // 220ms outward pop
                double hoverDuration = 260.0; // 260ms hover around exploded pot ("after a tiny bit")

                for (int i = 0; i < coinCount; i++)
                {
                    int val = baseVal + (i < rem ? 1 : 0);

                    // Evenly distribute in a ring with organic jitter
                    double angle = (i / (double)coinCount) * Math.PI * 2.0 + (Global.rng.NextDouble() - 0.5) * 0.25;
                    float burstRadius = (float)Global.rng.Next(75, 135);

                    // Perspective ellipse
                    float burstX = origin.X + (float)Math.Cos(angle) * burstRadius;
                    float burstY = origin.Y + (float)Math.Sin(angle) * (burstRadius * 0.78f);
                    PointF burstTarget = new PointF(burstX, burstY);

                    // Staggered launch towards window after the hover period
                    double flightDelay = i * 15.0;
                    double flightDuration = Global.rng.Next(330, 390);

                    // Arced flight path towards ransom cash target
                    float midX = (burstX + target.X) / 2f + (float)Global.rng.Next(-25, 26);
                    float midY = Math.Min(burstY, target.Y) - (float)Global.rng.Next(85, 175);

                    activeCoins.Add(new FlyingCoin
                    {
                        Origin = origin,
                        BurstTarget = burstTarget,
                        ControlPoint = new PointF(midX, midY),
                        Target = target,
                        StartTimeMs = curTime,
                        BurstDurationMs = burstDuration,
                        HoverDurationMs = hoverDuration,
                        FlightDelayMs = flightDelay,
                        FlightDurationMs = flightDuration,
                        PitchIndex = i % 6,
                        Value = val,
                        CurrentPos = origin,
                        LastDrawnRect = new Rectangle(origin.X - this.Left - 16, origin.Y - this.Top - 16, 32, 32)
                    });
                }
            }

            if (!animationTimer.Enabled)
                animationTimer.Start();
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (this.IsDisposed) return;

            double now = stopwatch.Elapsed.TotalMilliseconds;
            bool hasActiveElements = false;

            lock (stateLock)
            {
                Point currentTarget = GetCurrentRansomTarget();

                // --- 1. Update Flying Coins ---
                for (int i = activeCoins.Count - 1; i >= 0; i--)
                {
                    var coin = activeCoins[i];
                    if (coin.HasLanded)
                    {
                        this.Invalidate(coin.LastDrawnRect);
                        activeCoins.RemoveAt(i);
                        continue;
                    }

                    hasActiveElements = true;
                    double elapsed = now - coin.StartTimeMs;

                    if (coin.BurstDurationMs > 0 && elapsed < coin.BurstDurationMs)
                    {
                        // Phase 1: Outward burst pop around the pot
                        double p = Math.Clamp(elapsed / coin.BurstDurationMs, 0.0, 1.0);
                        double ease = 1.0 - Math.Pow(1.0 - p, 2.8);

                        float curX = coin.Origin.X + (coin.BurstTarget.X - coin.Origin.X) * (float)ease;
                        float curY = coin.Origin.Y + (coin.BurstTarget.Y - coin.Origin.Y) * (float)ease;
                        coin.CurrentPos = new PointF(curX, curY);
                    }
                    else if (coin.BurstDurationMs > 0 && elapsed < (coin.BurstDurationMs + coin.HoverDurationMs + coin.FlightDelayMs))
                    {
                        // Phase 2: Hover around where the pot exploded ("after a tiny bit")
                        coin.CurrentPos = coin.BurstTarget;
                    }
                    else
                    {
                        // Phase 3: Fly towards ransom cash target
                        double flightElapsed = (coin.BurstDurationMs > 0)
                            ? elapsed - (coin.BurstDurationMs + coin.HoverDurationMs + coin.FlightDelayMs)
                            : elapsed - coin.FlightDelayMs;

                        if (flightElapsed >= 0)
                        {
                            double progress = Math.Clamp(flightElapsed / coin.FlightDurationMs, 0.0, 1.0);
                            coin.Target = currentTarget;

                            double s = Math.Pow(progress, 2.6);
                            float invS = 1f - (float)s;

                            PointF startPt = (coin.BurstDurationMs > 0) ? coin.BurstTarget : (PointF)coin.Origin;
                            float curX = invS * invS * startPt.X + 2f * invS * (float)s * coin.ControlPoint.X + (float)(s * s) * coin.Target.X;
                            float curY = invS * invS * startPt.Y + 2f * invS * (float)s * coin.ControlPoint.Y + (float)(s * s) * coin.Target.Y;

                            coin.CurrentPos = new PointF(curX, curY);

                            if (progress >= 1.0)
                            {
                                coin.HasLanded = true;
                                coin.LandTimeMs = now;

                                CoinSoundSynthesizer.PlayCoinDing(coin.PitchIndex);
                                Ransomed.ActiveInstance?.OnSmallCoinArrived(coin.Value);

                                for (int sp = 0; sp < 6; sp++)
                                {
                                    double angle = Global.rng.NextDouble() * Math.PI * 2.0;
                                    float speed = (float)(Global.rng.NextDouble() * 3.8 + 1.8);
                                    activeSparkles.Add(new SparkleParticle
                                    {
                                        Position = coin.Target,
                                        Velocity = new PointF((float)(Math.Cos(angle) * speed), (float)(Math.Sin(angle) * speed)),
                                        StartTimeMs = now,
                                        DurationMs = 180.0,
                                        Size = Global.rng.Next(3, 6),
                                        Color = (sp % 2 == 0) ? Color.FromArgb(255, 235, 60) : Color.White,
                                        LastDrawnRect = new Rectangle(coin.Target.X - this.Left - 6, coin.Target.Y - this.Top - 6, 12, 12)
                                    });
                                }
                            }
                        }
                    }

                    int clientCurX = (int)coin.CurrentPos.X - this.Left;
                    int clientCurY = (int)coin.CurrentPos.Y - this.Top;

                    Rectangle oldRect = coin.LastDrawnRect;
                    Rectangle newRect = new Rectangle(clientCurX - 16, clientCurY - 16, 32, 32);
                    coin.LastDrawnRect = newRect;

                    this.Invalidate(oldRect);
                    this.Invalidate(newRect);
                }

                // --- 2. Update Sparkles ---
                for (int i = activeSparkles.Count - 1; i >= 0; i--)
                {
                    var sparkle = activeSparkles[i];
                    double elapsed = now - sparkle.StartTimeMs;
                    if (elapsed >= sparkle.DurationMs)
                    {
                        this.Invalidate(sparkle.LastDrawnRect);
                        activeSparkles.RemoveAt(i);
                        continue;
                    }

                    hasActiveElements = true;
                    Rectangle oldSpRect = sparkle.LastDrawnRect;
                    sparkle.Position = new PointF(sparkle.Position.X + sparkle.Velocity.X,
                                                  sparkle.Position.Y + sparkle.Velocity.Y);

                    int clientSpX = (int)sparkle.Position.X - this.Left;
                    int clientSpY = (int)sparkle.Position.Y - this.Top;
                    Rectangle newSpRect = new Rectangle(clientSpX - 6, clientSpY - 6, 12, 12);
                    sparkle.LastDrawnRect = newSpRect;

                    this.Invalidate(oldSpRect);
                    this.Invalidate(newSpRect);
                }

                if (activeSparkles.Count > 0 || activeCoins.Count > 0)
                {
                    hasActiveElements = true;
                }
            }

            if (!hasActiveElements)
            {
                animationTimer.Stop();
                NativeMethods.TimeEndPeriod(1);
                this.Hide();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.Clear(this.TransparencyKey);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            double now = stopwatch.Elapsed.TotalMilliseconds;

            lock (stateLock)
            {
                // 1. Render Flying Coins
                foreach (var coin in activeCoins)
                {
                    if (coin.HasLanded) continue;
                    double elapsed = now - coin.StartTimeMs;
                    if (coin.BurstDurationMs == 0 && elapsed < coin.FlightDelayMs) continue;

                    int cx = (int)(coin.CurrentPos.X - this.Left - 14);
                    int cy = (int)(coin.CurrentPos.Y - this.Top - 14);

                    g.DrawImageUnscaled(cachedSmallCoin, cx, cy);
                }

                // 2. Render Sparkles
                foreach (var sp in activeSparkles)
                {
                    double p = Math.Clamp((now - sp.StartTimeMs) / sp.DurationMs, 0.0, 1.0);
                    if (p >= 1.0) continue;

                    using var b = new SolidBrush(sp.Color);
                    int sz = (int)Math.Max(2, sp.Size * (1.0 - p));
                    int sx = (int)(sp.Position.X - this.Left);
                    int sy = (int)(sp.Position.Y - this.Top);
                    g.FillRectangle(b, sx - sz / 2, sy - sz / 2, sz, sz);
                }
            }
        }

        private class FlyingCoin
        {
            public Point Origin { get; set; }
            public PointF BurstTarget { get; set; }
            public Point Target { get; set; }
            public PointF ControlPoint { get; set; }
            public double StartTimeMs { get; set; }
            public double BurstDurationMs { get; set; }
            public double HoverDurationMs { get; set; }
            public double FlightDelayMs { get; set; }
            public double FlightDurationMs { get; set; }
            public int PitchIndex { get; set; }
            public int Value { get; set; }
            public bool HasLanded { get; set; }
            public double LandTimeMs { get; set; }
            public PointF CurrentPos { get; set; }
            public Rectangle LastDrawnRect { get; set; }
        }

        private class SparkleParticle
        {
            public PointF Position { get; set; }
            public PointF Velocity { get; set; }
            public double StartTimeMs { get; set; }
            public double DurationMs { get; set; }
            public float Size { get; set; }
            public Color Color { get; set; }
            public Rectangle LastDrawnRect { get; set; }
        }
    }
}
