using NAudio.Wave;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace rans0m
{
    public partial class Overlay : Form
    {
        private NotifyIcon? trayIcon;
        private System.Windows.Forms.Timer? topMostTimer;

        // Seamless animated continuous red static curved screen vignette
        private DoubleBufferedPictureBox? vignetteOverlay;
        private System.Windows.Forms.Timer? vignetteTimer;
        private Bitmap[]? activeVignetteFrames;
        private Bitmap[]? nextPreparedFrames;
        private readonly object vignetteLock = new();
        private CancellationTokenSource? vignetteCts;
        private Task? vignetteProducerTask;
        private double currentProgress = 0.0;
        private double targetProgress = 0.0;
        private int vignetteFrameIndex = 0;
        private WaveOut? preloadedLayer1;
        private WaveOut? preloadedLayer2;
        private WaveOut? preloadedLayer3;
        private readonly List<Rectangle> downloadStopSignRects = new();
        private readonly object downloadStopSignsLock = new();
        private static readonly Bitmap cachedStopSign = Properties.Resources.stop_sign;
        private bool isDownloading = false;
        private Point downloadTextJitter = Point.Empty;
        private Bitmap[]? downloadBgFrames;
        private Bitmap? brightRedFrame;
        private int downloadBgFrameIndex = 0;
        private Bitmap? CurrentDownloadBg => (downloadBgFrames != null && downloadBgFrames.Length > 0)
            ? downloadBgFrames[downloadBgFrameIndex % downloadBgFrames.Length]
            : null;

        private void EnsureDownloadBgFrames()
        {
            if (downloadBgFrames != null && downloadBgFrames.Length > 0 && brightRedFrame != null) return;
            try
            {
                int screenW = Math.Max(1920, SystemInformation.VirtualScreen.Width);
                int screenH = Math.Max(1080, SystemInformation.VirtualScreen.Height);
                int frameCount = 4;

                using (var tile = Properties.Resources.download_bg)
                {
                    int tileW = tile.Width;
                    int tileH = tile.Height;

                    if (downloadBgFrames == null || downloadBgFrames.Length == 0)
                    {
                        downloadBgFrames = new Bitmap[frameCount];

                        // Offsets for animated boiling textured crimson background
                        int[,] offsets = new int[,] {
                            { 0, 0 },
                            { tileW / 4, tileH / 4 },
                            { tileW / 2, tileH / 2 },
                            { 3 * tileW / 4, 3 * tileH / 4 }
                        };

                        for (int f = 0; f < frameCount; f++)
                        {
                            var bmp = new Bitmap(screenW, screenH, PixelFormat.Format32bppArgb);
                            using (var g = Graphics.FromImage(bmp))
                            using (var brush = new TextureBrush(tile, WrapMode.Tile))
                            {
                                brush.TranslateTransform(offsets[f, 0], offsets[f, 1]);
                                g.FillRectangle(brush, 0, 0, screenW, screenH);
                            }
                            downloadBgFrames[f] = bmp;
                        }
                    }

                    if (brightRedFrame == null)
                    {
                        var bmp = new Bitmap(screenW, screenH, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(bmp))
                        using (var brush = new SolidBrush(Color.FromArgb(245, 20, 20)))
                        {
                            g.FillRectangle(brush, 0, 0, screenW, screenH);
                        }
                        brightRedFrame = bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error preparing downloadBgFrames: {ex.Message}");
            }
        }

        private static class NativeMethods
        {
            public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOACTIVATE = 0x0010;

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        }

        // ----------------------------- CONSTRUCTOR AND OVERLAY SETUP -----------------------------
        public static Overlay? Instance { get; private set; }
        public int CurrentVignetteStage => Math.Clamp((int)(currentProgress * 4.0) + 1, 1, 5);
        public double CurrentVignetteProgress => currentProgress;

        public Overlay()
        {
            Instance = this;
            InitializeComponent();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }






        // ----------------------------- RANSOM PHASES -----------------------------

        /// <summary>
        /// First phase of Ransom
        /// </summary>
        /// <returns>true if the mouse moved during the warning phase</returns>
        public async Task<bool> RansomWarning()
        {
            WaveOut spawnSound = SoundHelper.Create(Properties.Resources.spawn);
            spawnSound.Play();

            // Shows Ransom's face randomly on the screen
            Global.RandomPosControl(pc_ransom);
            pc_ransom.Visible = true;

            await Task.Delay(500);
            // Mouse spy phase
            Global.lastRegisteredMousePos = MousePosition;
            Global.spyingMouse = true;

            // Hide Ransom's face and show the warning sign
            pc_stopsign.Visible = true;
            pc_ransom.Visible = false;

            await Task.Delay(500);
            // End of spy phase

            Global.spyingMouse = false;

            // Hide the warning sign, show Ransom face's on the center with a red background
            pc_stopsign.Visible = false;
            pc_ransom.Visible = true;
            Global.CenterControl(pc_ransom);
            this.BackColor = Color.DarkRed;

            await Task.Delay(100);

            this.BackColor = this.TransparencyKey;
            pc_ransom.Visible = false;

            return MousePosition != Global.lastRegisteredMousePos; // Returns true if the mouse moved during the spy phase
        }

        /// <summary>
        /// Second phase of Ransom, Jumpscare+Downloading effect
        /// </summary>
        public async Task DownloadJumpscare()
        {
            WaveOut attackSound = SoundHelper.Create(Properties.Resources.attack);
            attackSound.Play();

            Global.CenterControl(pc_attack);
            Point attack_center = pc_attack.Location; // Store the center position of the attack image for the shake effect
            pc_attack.Visible = true;
            this.BackColor = Color.DarkRed;

            // Ransom Face Shake Effect
            new Thread(async () =>
            {
                for (int i = 0; i <= 25; i++)
                {
                    await Task.Delay(20);
                    try { this.Invoke(() => pc_attack.Location = new Point(attack_center.X + Global.rng.Next(-40, 40), attack_center.Y + Global.rng.Next(-40, 40))); }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            // Start applying checkered wallpaper to desktop in background so it is active underneath when download completes
            _ = Task.Run(WallpaperManager.ApplyRansomWallpaper);

            await Task.Delay(800);

            // Downloading screen
            pc_ransom.Visible = false;
            pc_attack.Visible = false;
            this.BackColor = this.TransparencyKey;

            // Display textured crimson background from the game directly on vignetteOverlay
            EnsureDownloadBgFrames();
            if (vignetteOverlay != null && downloadBgFrames != null && downloadBgFrames.Length > 0)
            {
                vignetteTimer?.Stop();
                downloadBgFrameIndex = 0;
                vignetteOverlay.Image = downloadBgFrames[0];
                vignetteOverlay.Visible = true;
                vignetteOverlay.BringToFront();
            }

            WaveOut installSound = SoundHelper.Create(Properties.Resources.install);
            installSound.PlaybackStopped += (s, e) =>
            {
                try { installSound.Dispose(); } catch { }
            };
            installSound.Play();

            // Pre-load layer1/layer2/layer3 on worker threads during download wait for instantaneous OST playback
            Task.Run(() =>
            {
                try
                {
                    preloadedLayer1 = SoundHelper.Create(Properties.Resources.layer1);
                    preloadedLayer2 = SoundHelper.Create(Properties.Resources.layer2);
                    preloadedLayer3 = SoundHelper.Create(Properties.Resources.layer3);
                }
                catch { }
            });

            // Center progress bar
            pb_download.Size = new Size(476, 28);
            pb_download.FilledSegments = 0; // Starts empty
            Global.CenterControl(pb_download);

            // Hide txt_download so no transparent WinForms label punches holes in the red background
            // "DOWNLOADING" is drawn directly via GDI+ inside vignetteOverlay.Paint
            txt_download.Visible = false;
            pb_download.Visible = true;
            pb_download.BringToFront();

            Point basePb = pb_download.Location;

            isDownloading = true;
            downloadTextJitter = Point.Empty;

            lock (downloadStopSignsLock)
            {
                downloadStopSignRects.Clear();
            }

            void AddRandomStopSign()
            {
                int w = (vignetteOverlay != null && vignetteOverlay.ClientSize.Width > 0) ? vignetteOverlay.ClientSize.Width : 1920;
                int h = (vignetteOverlay != null && vignetteOverlay.ClientSize.Height > 0) ? vignetteOverlay.ClientSize.Height : 1080;
                int size = Global.rng.Next(110, 310);
                int maxX = Math.Max(1, w - size);
                int maxY = Math.Max(1, h - size);
                int x = Global.rng.Next(0, maxX);
                int y = Global.rng.Next(0, maxY);

                lock (downloadStopSignsLock)
                {
                    if (downloadStopSignRects.Count < 60)
                    {
                        downloadStopSignRects.Add(new Rectangle(x, y, size, size));
                    }
                }
            }

            // Initial burst of 8 to 10 stop signs across the screen immediately
            for (int i = 0; i < 9; i++)
            {
                AddRandomStopSign();
            }
            vignetteOverlay?.Invalidate();

            // Animate boiling textured crimson background during download
            new Thread(() =>
            {
                while (isDownloading)
                {
                    Thread.Sleep(45); // ~22 FPS texture crawl/boil
                    if (!isDownloading) break;
                    try
                    {
                        this.Invoke(() =>
                        {
                            if (!isDownloading || vignetteOverlay == null || downloadBgFrames == null || downloadBgFrames.Length == 0) return;
                            downloadBgFrameIndex = (downloadBgFrameIndex + 1) % downloadBgFrames.Length;
                            vignetteOverlay.Image = downloadBgFrames[downloadBgFrameIndex];
                            vignetteOverlay.Invalidate();
                        });
                    }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            // Spawning background stop signs: smoothly reaches exactly ~60 stop signs total, appearing randomly and overlapping
            new Thread(async () =>
            {
                while (isDownloading)
                {
                    await Task.Delay(35);
                    if (!isDownloading) break;

                    try
                    {
                        this.Invoke(() =>
                        {
                            if (!isDownloading || vignetteOverlay == null) return;

                            lock (downloadStopSignsLock)
                            {
                                if (downloadStopSignRects.Count < 60)
                                {
                                    AddRandomStopSign();
                                    AddRandomStopSign();
                                    vignetteOverlay.Invalidate();
                                }
                            }
                        });
                    }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            // Glitch jitter in background thread while downloading
            new Thread(() =>
            {
                while (isDownloading)
                {
                    Thread.Sleep(35);
                    try
                    {
                        this.Invoke(() =>
                        {
                            if (!isDownloading) return;
                            downloadTextJitter = new Point(Global.rng.Next(-2, 3), Global.rng.Next(-2, 3));
                            pb_download.Location = new Point(basePb.X + Global.rng.Next(-2, 3), basePb.Y + Global.rng.Next(-2, 3));
                            vignetteOverlay?.Invalidate();
                        });
                    }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            // Fast, punchy download animation: 10 segments fill over ~900ms (authentic game speed)
            for (int seg = 1; seg <= 10; seg++)
            {
                await Task.Delay(90);
                pb_download.FilledSegments = seg;
                pb_download.Refresh();
            }

            // Quick hold at full bar (15ms)
            await Task.Delay(15);

            // Stop downloading & restore base positions
            isDownloading = false;
            pb_download.Location = basePb;

            // Hide download UI & clear stop signs
            pb_download.Visible = false;
            txt_download.Visible = false;

            lock (downloadStopSignsLock)
            {
                downloadStopSignRects.Clear();
            }
            vignetteOverlay?.Invalidate();

            // Small impact when download finishes: punchy impact sound + bright red flash for a brief moment
            CoinSoundSynthesizer.PlayWindowSlamGlitch(0.85f);
            if (vignetteOverlay != null && brightRedFrame != null)
            {
                vignetteOverlay.Image = brightRedFrame;
                vignetteOverlay.Refresh();
            }

            await Task.Delay(85);

            // Red background immediately goes away, switching to the static border effect and showing the windows
            StartVignette();
            vignetteOverlay?.Refresh();
        }

        /// <summary>
        /// Third phase of Ransom, the actual ransom, plays the music, shows the Ransomed window, etc...
        /// </summary>
        public async Task<bool> Ransomed()
        {
            // Ensure wallpaper is active without blocking the UI thread
            _ = Task.Run(WallpaperManager.ApplyRansomWallpaper);

            // Ensure vignette is active and running at initial baseline
            StartVignette();

            // OST (uses pre-loaded instances so playback starts in 0ms with zero stutter)
            WaveOut layer1 = preloadedLayer1 ?? SoundHelper.Create(Properties.Resources.layer1);
            WaveOut layer2 = preloadedLayer2 ?? SoundHelper.Create(Properties.Resources.layer2);
            WaveOut layer3 = preloadedLayer3 ?? SoundHelper.Create(Properties.Resources.layer3);
            preloadedLayer1 = null;
            preloadedLayer2 = null;
            preloadedLayer3 = null;

            Global.ransomLeft = 500;
            Global.underRansom = true;
            Global.StopMusic = () =>
            {
                try
                {
                    if (layer1 != null && layer1.PlaybackState == PlaybackState.Playing)
                    {
                        layer1.Volume = 0f;
                        layer1.Stop();
                    }
                }
                catch { }
                try
                {
                    if (layer2 != null && layer2.PlaybackState == PlaybackState.Playing)
                    {
                        layer2.Volume = 0f;
                        layer2.Stop();
                    }
                }
                catch { }
                try
                {
                    if (layer3 != null && layer3.PlaybackState == PlaybackState.Playing)
                    {
                        layer3.Volume = 0f;
                        layer3.Stop();
                    }
                }
                catch { }
            };

            Global.RansomPayed = () => // Ransom payed event
            {
                Global.StopMusic?.Invoke();
                this.Invoke(() => ResetRansom(animateVignette: true));
            };

            // Random flashing Ransom faces
            new Thread(async () =>
            {
                while (Global.underRansom)
                {
                    await Task.Delay(Global.rng.Next(5000));
                    try
                    {
                        this.Invoke((MethodInvoker)async delegate
                        {
                            for (int i = 0; i <= Global.rng.Next(1, 5); i++)
                            {
                                PictureBox ransomFace = new PictureBox();
                                ransomFace.Image = Properties.Resources.ransom_random;
                                int size = Global.rng.Next(50, 400);
                                ransomFace.Size = new Size(size, size);

                                Global.RandomPosControl(ransomFace);
                                ransomFace.SizeMode = PictureBoxSizeMode.StretchImage;

                                this.Controls.Add(ransomFace);
                                await Task.Delay(25);
                                this.Controls.Remove(ransomFace);
                            }
                        });
                    }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            // Shows the main ransom window owned by Overlay so it stays on top
            Ransomed ransomedForm = new Ransomed();
            ransomedForm.Show(this);

            StartVignette();

            // Dynamic swarm of taunt windows: starts with 6-7 windows, then settles down to 2 active windows (not counting main window)
            new Thread(async () =>
            {
                Stopwatch tauntStopwatch = Stopwatch.StartNew();
                while (Global.underRansom)
                {
                    await Task.Delay(Global.rng.Next(500, 900));
                    if (!Global.underRansom) break;

                    try
                    {
                        this.Invoke(() =>
                        {
                            if (!Global.underRansom) return;
                            lock (Global.activeTauntWindows)
                            {
                                Global.activeTauntWindows.RemoveAll(f => f == null || f.IsDisposed);
                                int activeCount = Global.activeTauntWindows.Count;

                                // Initial swarm for first ~7.5 seconds, then settle down to exactly 2 active taunt windows (not counting main window)
                                int targetTaunts = (tauntStopwatch.ElapsedMilliseconds < 7500) ? 6 : 2;

                                if (activeCount < targetTaunts)
                                {
                                    int toSpawn = targetTaunts - activeCount;
                                    for (int s = 0; s < toSpawn; s++)
                                    {
                                        TauntWindow tw = new TauntWindow();
                                        tw.Show();
                                    }
                                }
                            }
                        });
                    }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            layer1.Play();
            await Task.Delay(26000);
            if (!Global.underRansom) return false;

            layer2.Play();
            await Task.Delay(26000);
            if (!Global.underRansom) return false;

            layer3.Play();
            await Task.Delay(26000);
            if (!Global.underRansom) return false;

            // If the code reaches here, this means the user didn't pay the ransom in time 
            WallpaperManager.RestoreOriginalWallpaper();
            StopVignette();
            ransomedForm.Close();
            return true;
        }

        /// <summary>
        /// Shows the jumpscare and crashes/shutdowns the computer depending if it's started as admin or not
        /// </summary>
        public async Task CrashJumpscare()
        {
            WaveOut attackSound = SoundHelper.Create(Properties.Resources.attack);
            attackSound.Play();

            this.BackColor = Color.DarkRed;
            Global.CenterControl(pc_attack);
            Point attack_center = pc_attack.Location; // Store the center position of the attack image for the shake effect
            pc_attack.Visible = true;
            // Shake effect
            new Thread(async () =>
            {
                for (int i = 0; i <= 25; i++)
                {
                    await Task.Delay(10);
                    try { this.Invoke(() => pc_attack.Location = new Point(attack_center.X + Global.rng.Next(-40, 40), attack_center.Y + Global.rng.Next(-40, 40))); }
                    catch { break; }
                }
            }) { IsBackground = true }.Start();

            await Task.Delay(1000);

            if (Global.IsAdministrator())
            {
                // Basically morphs the process into a critical process, then kills it, causing a BSOD
                WallpaperManager.RestoreOriginalWallpaper();
                Global.IntoCriticalProcess();
                this.Close();
            } 
            else
            {
                // If not admin, restore wallpaper and shutdown the computer
                WallpaperManager.RestoreOriginalWallpaper();
                Process.Start("shutdown", "/s /t 0");
            }
        }








        // ----------------------------- CORE -----------------------------

        /// <summary>
        /// Reset the app to it's ready state
        /// </summary>
        public void ResetRansom(bool animateVignette = false)
        {
            WallpaperManager.RestoreOriginalWallpaper();
            if (animateVignette)
            {
                _ = AnimateVignetteExitAsync();
            }
            else
            {
                StopVignette();
            }

            lock (Global.activeTauntWindows)
            {
                foreach (var tw in Global.activeTauntWindows.ToArray())
                {
                    try { tw.Close(); tw.Dispose(); } catch { }
                }
                Global.activeTauntWindows.Clear();
            }

            GoldCoinManager.DeleteAllCoins();
            Global.canAttack = true;
            Global.RansomPayed = null;
            Global.StopMusic = null;
            Global.underRansom = false;
            Global.ransomLeft = 0;

            isDownloading = false;
            lock (downloadStopSignsLock)
            {
                downloadStopSignRects.Clear();
            }

            txt_download.Font = new Font("Arial", 22F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txt_download.Text = "DOWNLOADING";
            pc_ransom.Visible = false;
            pc_attack.Visible = false;
            txt_download.Visible = false;
            pb_download.Visible = false;
            pc_stopsign.Visible = false;
            pb_download.Value = 0;
            this.BackgroundImage = null;
            this.BackColor = this.TransparencyKey;
        }

        /// <summary>
        /// Summons Ransom, triggers each step of the ransom process
        /// </summary>
        public async void SpawnRansom()
        {
            if (!Global.canAttack) return;
            Global.canAttack = false;

            bool mouseMoved = await RansomWarning();
            if (mouseMoved) // User moved the mouse
            {
                try { GoldCoinManager.CreateRandomCoins(8); }
                catch { }

                await DownloadJumpscare();

                if (await Ransomed()) // If user didn't pay the ransom in time
                {
                    Global.underRansom = false;
                    await CrashJumpscare();
                    this.Close();
                }
            }
            else ResetRansom(); // User didn't move the mouse, dodged the ransom
        }

        /// <summary>
        /// Ransom loop, keeps summoning ransom randomly
        /// </summary>
        private async void RansomLoop()
        {
            while (!this.IsDisposed)
            {
                try { this.Invoke(SpawnRansom); } // Invoke via the UI thread to avoid cross-thread exceptions (RansomLoop will be called only from another thread soooo)
                catch { break; }

                await Task.Delay(Global.rng.Next(Global.minRansomTime*1000, Global.maxRansomTime*1000)); // Ransom Debounce
            }
        }

        /// <summary>
        /// Setup the TrayIcon to close the app (cause there's no other way to close it)
        /// </summary>
        private void SetupTrayIcon()
        {
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Close").Click += (s, e) =>
            {
                if (!Global.canAttack) return;
                Close();
            };

            trayIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Text = "RANS0M",
                Visible = true
            };

            this.FormClosed += (s, e) =>
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            };
        }

        // ----------------------------- EVENT HANDLERS -----------------------------

        private void Overlay_Load(object sender, EventArgs e)
        {
            // Set the overlay to cover the entire screen
            this.Bounds = SystemInformation.VirtualScreen; // Idk if ScreenBounds or this is better
            this.Location = new Point(0, 0);

            // Register .gold and .pot files and set the app to it's ready state
            FileTypeRegister.RegisterIconForExtension(".gold", Properties.Resources.GoldIco, "GoldFile");
            FileTypeRegister.RegisterBitmapForExtension(".pot", Properties.Resources.honey_pot, "HoneyPotFile");
            ResetRansom();
            SetupVignettes();
            SetupTrayIcon();
            SetupTopMostTimer();

            // Starts the RansomLoop (in a new thread to avoid blocking the UI thread)
            new Thread(RansomLoop) { IsBackground = true }.Start();
        }

        /// <summary>
        /// TopMost alone doesn't stick, other windows (especially elevated ones) can still cover the overlay,
        /// so this keeps shoving it back to the front without stealing focus/keyboard input
        /// </summary>
        private void SetupTopMostTimer()
        {
            topMostTimer = new System.Windows.Forms.Timer { Interval = 500 };
            topMostTimer.Tick += (s, e) =>
            {
                if (IsDisposed) return;
                if (Global.underRansom) return; // Do not cover Ransomed dialog or taunts while under ransom!
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            };
            topMostTimer.Start();

            this.FormClosed += (s, e) =>
            {
                topMostTimer.Stop();
                WallpaperManager.RestoreOriginalWallpaper();
                if (downloadBgFrames != null)
                {
                    foreach (var b in downloadBgFrames) { try { b.Dispose(); } catch { } }
                    downloadBgFrames = null;
                }
                if (brightRedFrame != null)
                {
                    try { brightRedFrame.Dispose(); } catch { }
                    brightRedFrame = null;
                }
            };
        }

        private void Overlay_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Global.underRansom) // If the user is under ransom, prevent closing the overlay
            {
                e.Cancel = true;
                return;
            }
            WallpaperManager.RestoreOriginalWallpaper();
        }

        // ----------------------------- VIGNETTE (CONTINUOUS ANIMATED CURVED STATIC BORDER) -----------------------------

        private void SetupVignettes()
        {
            vignetteOverlay = new DoubleBufferedPictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = Color.Transparent,
                Visible = false
            };

            this.Controls.Add(vignetteOverlay);
            vignetteOverlay.SendToBack();

            // Host download controls inside vignetteOverlay
            vignetteOverlay.Controls.Add(txt_download);
            vignetteOverlay.Controls.Add(pb_download);

            vignetteOverlay.Paint += (sender, pe) =>
            {
                if (!isDownloading) return;

                Graphics g = pe.Graphics;
                g.InterpolationMode = InterpolationMode.Low;

                // Draw all accumulated stop signs in spawn order so newer ones naturally overlap older ones
                lock (downloadStopSignsLock)
                {
                    for (int i = 0; i < downloadStopSignRects.Count; i++)
                    {
                        g.DrawImage(cachedStopSign, downloadStopSignRects[i]);
                    }
                }

                // Draw crisp "DOWNLOADING" text directly above the progress bar with glitch jitter
                using (var brush = new SolidBrush(Color.White))
                using (var font = new Font("Arial", 22F, FontStyle.Bold, GraphicsUnit.Point))
                {
                    var textSize = g.MeasureString("DOWNLOADING", font);
                    float tx = pb_download.Location.X + (pb_download.Width - textSize.Width) / 2f + downloadTextJitter.X;
                    float ty = pb_download.Location.Y - textSize.Height - 14f + downloadTextJitter.Y;
                    g.DrawString("DOWNLOADING", font, brush, tx, ty);
                }
            };

            // Pre-generate download background textures
            EnsureDownloadBgFrames();

            // Pre-warm checkered wallpaper bitmap in background so it is instantly available when applied
            _ = Task.Run(() => WallpaperManager.GetCheckeredWallpaperBitmap());

            // Pre-generate initial Step 0 baseline frames so they are ready on startup
            activeVignetteFrames = GenerateStepFrames(0.0, 42);

            vignetteTimer = new System.Windows.Forms.Timer { Interval = 45 };
            vignetteTimer.Tick += (s, e) =>
            {
                if (vignetteOverlay == null) return;

                lock (vignetteLock)
                {
                    // If the background producer has prepared the next progressive micro-stage, swap it in!
                    if (nextPreparedFrames != null)
                    {
                        var old = activeVignetteFrames;
                        activeVignetteFrames = nextPreparedFrames;
                        nextPreparedFrames = null;
                        if (old != null)
                        {
                            foreach (var b in old)
                            {
                                try { b.Dispose(); } catch { }
                            }
                        }
                    }

                    if (activeVignetteFrames != null && activeVignetteFrames.Length > 0)
                    {
                        // Random frame stepping to create a non-repeating boiling static effect
                        vignetteFrameIndex = (vignetteFrameIndex + Global.rng.Next(1, 4)) % activeVignetteFrames.Length;
                        vignetteOverlay.Image = activeVignetteFrames[vignetteFrameIndex];
                    }
                }
            };
        }

        private void StartVignette()
        {
            if (vignetteOverlay == null) return;

            lock (vignetteLock)
            {
                if (activeVignetteFrames == null || activeVignetteFrames.Length == 0)
                {
                    activeVignetteFrames = GenerateStepFrames(0.0, 42);
                }
                if (activeVignetteFrames != null && activeVignetteFrames.Length > 0)
                {
                    vignetteOverlay.Image = activeVignetteFrames[0];
                }
            }

            vignetteOverlay.Visible = true;
            vignetteOverlay.BringToFront();

            currentProgress = 0.0;
            Volatile.Write(ref targetProgress, 0.0);
            if (vignetteTimer != null) vignetteTimer.Interval = 45;
            vignetteTimer?.Start();

            StartVignetteProducer();
        }

        public void UpdateVignetteProgress(double progress)
        {
            currentProgress = Math.Clamp(progress, 0.0, 1.0);
            Volatile.Write(ref targetProgress, currentProgress);

            // Continuously speed up the boiling timer: 45ms down to 12ms
            int interval = Math.Clamp((int)(45.0 - currentProgress * 33.0), 12, 45);
            if (vignetteTimer != null) vignetteTimer.Interval = interval;
        }

        public void SetVignetteStage(int stage)
        {
            double p = Math.Clamp((stage - 1.0) / 4.0, 0.0, 1.0);
            UpdateVignetteProgress(p);
        }

        private void StartVignetteProducer()
        {
            StopVignetteProducer();

            vignetteCts = new CancellationTokenSource();
            var token = vignetteCts.Token;

            vignetteProducerTask = Task.Run(() =>
            {
                double lastPreparedProgress = -1.0;
                int stepSeed = 100;

                while (!token.IsCancellationRequested)
                {
                    double curTarget = Volatile.Read(ref targetProgress);

                    bool needsPrep = false;
                    lock (vignetteLock)
                    {
                        if (nextPreparedFrames == null && Math.Abs(curTarget - lastPreparedProgress) > 0.005)
                        {
                            needsPrep = true;
                        }
                    }

                    if (needsPrep)
                    {
                        stepSeed += 31;
                        Bitmap[] generated = GenerateStepFrames(curTarget, stepSeed);

                        lock (vignetteLock)
                        {
                            if (!token.IsCancellationRequested)
                            {
                                nextPreparedFrames = generated;
                                lastPreparedProgress = curTarget;
                            }
                            else
                            {
                                foreach (var b in generated) { try { b.Dispose(); } catch { } }
                            }
                        }
                    }

                    try { Task.Delay(25, token).Wait(); }
                    catch { break; }
                }
            }, token);
        }

        private void StopVignetteProducer()
        {
            try
            {
                vignetteCts?.Cancel();
                vignetteCts?.Dispose();
            }
            catch { }
            vignetteCts = null;
            vignetteProducerTask = null;
        }

        public async Task AnimateVignetteExitAsync()
        {
            if (vignetteOverlay == null || !vignetteOverlay.Visible)
            {
                StopVignette();
                return;
            }

            vignetteTimer?.Stop();
            StopVignetteProducer();

            // 4 discrete, snappy corrupt retraction steps: reach shrinks outward toward the perimeter
            double[] factors = new double[] { 0.65, 0.38, 0.16, 0.04 };
            for (int i = 0; i < factors.Length; i++)
            {
                try
                {
                    Bitmap exitFrame = GenerateExitFrame(factors[i], 777 + i * 43);
                    var oldImage = vignetteOverlay.Image;
                    vignetteOverlay.Image = exitFrame;
                    vignetteOverlay.Refresh();
                    if (oldImage != null && oldImage != brightRedFrame)
                    {
                        try { oldImage.Dispose(); } catch { }
                    }
                }
                catch { }

                await Task.Delay(60);
            }

            StopVignette();
        }

        private Bitmap GenerateExitFrame(double exitScale, int seed)
        {
            int screenW = Math.Max(1920, SystemInformation.VirtualScreen.Width);
            int screenH = Math.Max(1080, SystemInformation.VirtualScreen.Height);
            Color transKey = this.TransparencyKey;
            byte tB = transKey.B;
            byte tG = transKey.G;
            byte tR = transKey.R;

            double p = Math.Clamp(currentProgress, 0.0, 1.0);
            double baseCorner = 135.0 + p * (460.0 - 135.0);
            double baseLr = 50.0 + p * (215.0 - 50.0);
            double baseTb = 32.0 + p * (140.0 - 32.0);

            double rCorner = baseCorner * exitScale;
            double rLr = baseLr * exitScale;
            double rTb = baseTb * exitScale;
            int maxReach = (int)Math.Ceiling(rCorner) + 2;

            Bitmap frame = new Bitmap(screenW, screenH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = frame.LockBits(new Rectangle(0, 0, screenW, screenH),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            int stride = data.Stride;
            IntPtr ptr = data.Scan0;
            int totalBytes = Math.Abs(stride) * screenH;
            byte[] rgb = new byte[totalBytes];

            // Fast pre-fill with transKey so center area is 100% transparent with zero work
            for (int i = 0; i < totalBytes; i += 4)
            {
                rgb[i] = tB;
                rgb[i + 1] = tG;
                rgb[i + 2] = tR;
                rgb[i + 3] = 255;
            }

            if (exitScale > 0.01 && maxReach > 0)
            {
                Random rnd = new Random(seed);
                for (int y = 0; y < screenH; y++)
                {
                    int dy = Math.Min(y, screenH - 1 - y);
                    bool inYReach = (dy <= maxReach);
                    int rowOff = y * stride;
                    double v = Math.Max(0.0, 1.0 - dy / rTb);

                    int passes = inYReach ? 1 : 2;
                    for (int pass = 0; pass < passes; pass++)
                    {
                        int xStart = (pass == 0) ? 0 : screenW - maxReach;
                        int xEnd = (pass == 0) ? (inYReach ? screenW : maxReach) : screenW;

                        for (int x = xStart; x < xEnd; x++)
                        {
                            int dx = Math.Min(x, screenW - 1 - x);
                            double u = Math.Max(0.0, 1.0 - dx / rLr);
                            double dCorner = Math.Sqrt((dx / rCorner) * (dx / rCorner) + (dy / rCorner) * (dy / rCorner));
                            double c = Math.Max(0.0, 1.0 - dCorner);

                            double intensity = Math.Max(u, Math.Max(v, c));
                            if (intensity <= 0.0) continue;

                            double prob = Math.Pow(intensity, 1.7) * (0.82 * exitScale + 0.1);
                            if (y % 14 == 0) prob = Math.Min(0.95, prob * 1.35);

                            if (rnd.NextDouble() < prob)
                            {
                                byte pR, pG, pB;
                                PickGlitchColor(rnd, out pR, out pG, out pB);
                                int pix = rowOff + x * 4;
                                rgb[pix] = pB;
                                rgb[pix + 1] = pG;
                                rgb[pix + 2] = pR;
                            }
                        }
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(rgb, 0, ptr, totalBytes);
            frame.UnlockBits(data);
            return frame;
        }

        private void StopVignette()
        {
            vignetteTimer?.Stop();
            StopVignetteProducer();

            if (vignetteOverlay != null)
            {
                var oldImg = vignetteOverlay.Image;
                vignetteOverlay.Visible = false;
                vignetteOverlay.Image = null;
                if (oldImg != null && oldImg != brightRedFrame)
                {
                    try { oldImg.Dispose(); } catch { }
                }
            }

            lock (vignetteLock)
            {
                if (nextPreparedFrames != null)
                {
                    foreach (var b in nextPreparedFrames) { try { b.Dispose(); } catch { } }
                    nextPreparedFrames = null;
                }
            }
        }

        private Bitmap[] GenerateStepFrames(double progress, int seed)
        {
            int screenW = Math.Max(1920, SystemInformation.VirtualScreen.Width);
            int screenH = Math.Max(1080, SystemInformation.VirtualScreen.Height);
            Color transKey = this.TransparencyKey;
            byte tB = transKey.B;
            byte tG = transKey.G;
            byte tR = transKey.R;

            double p = Math.Clamp(progress, 0.0, 1.0);
            double rCorner = 135.0 + p * (460.0 - 135.0);
            double rLr = 50.0 + p * (215.0 - 50.0);
            double rTb = 32.0 + p * (140.0 - 32.0);
            int maxReach = (int)Math.Ceiling(rCorner) + 2;

            int framesCount = 4;
            Bitmap[] frames = new Bitmap[framesCount];

            Parallel.For(0, framesCount, f =>
            {
                Random rnd = new Random(seed + f * 101);
                Bitmap frame = new Bitmap(screenW, screenH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var data = frame.LockBits(new Rectangle(0, 0, screenW, screenH),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                int stride = data.Stride;
                IntPtr ptr = data.Scan0;
                int totalBytes = Math.Abs(stride) * screenH;
                byte[] rgb = new byte[totalBytes];

                // Fast pre-fill with transKey so center area is 100% transparent with zero work
                for (int i = 0; i < totalBytes; i += 4)
                {
                    rgb[i] = tB;
                    rgb[i + 1] = tG;
                    rgb[i + 2] = tR;
                    rgb[i + 3] = 255;
                }

                // Fine 1:1 pixel evaluation: only process the outer boundary within maxReach
                for (int y = 0; y < screenH; y++)
                {
                    int dy = Math.Min(y, screenH - 1 - y);
                    bool inYReach = (dy <= maxReach);
                    int rowOff = y * stride;
                    double v = Math.Max(0.0, 1.0 - dy / rTb);

                    int passes = inYReach ? 1 : 2;
                    for (int pass = 0; pass < passes; pass++)
                    {
                        int xStart = (pass == 0) ? 0 : screenW - maxReach;
                        int xEnd = (pass == 0) ? (inYReach ? screenW : maxReach) : screenW;

                        for (int x = xStart; x < xEnd; x++)
                        {
                            int dx = Math.Min(x, screenW - 1 - x);
                            double u = Math.Max(0.0, 1.0 - dx / rLr);
                            double dCorner = Math.Sqrt((dx / rCorner) * (dx / rCorner) + (dy / rCorner) * (dy / rCorner));
                            double c = Math.Max(0.0, 1.0 - dCorner);

                            double intensity = Math.Max(u, Math.Max(v, c));
                            if (intensity <= 0.0) continue;

                            // Strong, vibrant border density tapering into clear screen
                            double prob = Math.Pow(intensity, 1.7) * 0.82;
                            if ((y + f * 3) % 14 == 0) prob = Math.Min(0.95, prob * 1.35); // CRT scanline

                            if (rnd.NextDouble() < prob)
                            {
                                byte pR, pG, pB;
                                PickGlitchColor(rnd, out pR, out pG, out pB);
                                int pix = rowOff + x * 4;
                                rgb[pix] = pB;
                                rgb[pix + 1] = pG;
                                rgb[pix + 2] = pR;
                            }
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(rgb, 0, ptr, totalBytes);
                frame.UnlockBits(data);
                frames[f] = frame;
            });

            return frames;
        }

        private static void PickGlitchColor(Random rnd, out byte rR, out byte rG, out byte rB)
        {
            double cRnd = rnd.NextDouble();
            if (cRnd < 0.74) // Saturated digital CRT red
            {
                rR = (byte)rnd.Next(220, 256);
                rG = (byte)rnd.Next(10, 35);
                rB = (byte)rnd.Next(10, 35);
            }
            else if (cRnd < 0.88) // Deep crimson / maroon
            {
                rR = (byte)rnd.Next(140, 190);
                rG = (byte)rnd.Next(0, 15);
                rB = (byte)rnd.Next(0, 15);
            }
            else if (cRnd < 0.92) // Crisp white speck
            {
                rR = 255; rG = 255; rB = 255;
            }
            else if (cRnd < 0.96) // Electric cyan speck
            {
                rR = 25; rG = (byte)rnd.Next(225, 256); rB = 255;
            }
            else // Golden yellow speck
            {
                rR = 255; rG = (byte)rnd.Next(220, 255); rB = 25;
            }
        }
    }

    internal class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}
