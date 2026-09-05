using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace rans0m
{
    public static class WallpaperManager
    {
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDesktopWallpaper
        {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
            void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);
            void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorID);
            void GetMonitorDevicePathCount(out uint count);
            void GetWallpaperRect([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out IntPtr rect);
            void SetBackgroundColor(uint color);
            void GetBackgroundColor(out uint color);
            void SetPosition(int position);
            void GetPosition(out int position);
            void SetSlideshow(IntPtr items);
            void GetSlideshow(out IntPtr items);
            void SetSlideshowOptions(int options, uint slideshowTick);
            void GetSlideshowOptions(out int options, out uint slideshowTick);
            void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, int direction);
            void GetStatus(out int state);
            void Enable(bool enable);
        }

        private static readonly Guid CLSID_DesktopWallpaper = new Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");

        private static IDesktopWallpaper? GetDesktopWallpaperCOM()
        {
            try
            {
                Type? t = Type.GetTypeFromCLSID(CLSID_DesktopWallpaper);
                if (t != null)
                {
                    return (IDesktopWallpaper?)Activator.CreateInstance(t);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create IDesktopWallpaper COM object: {ex.Message}");
            }
            return null;
        }

        private class BackupData
        {
            public System.Collections.Generic.Dictionary<string, string> Monitors { get; set; } = new();
            public int Position { get; set; } = 4; // Default Fill
            public string? LegacyPath { get; set; }
            public string? LegacyStyle { get; set; }
            public string? LegacyTile { get; set; }
        }

        private const string RegistrySubKey = @"Software\RANSOM";
        private const string BackupValueName = "WallpaperBackup";

        private static BackupData? inMemoryBackup = null;
        private static string? generatedWallpaperPath = null;
        private static bool isRansomWallpaperActive = false;
        private static readonly object syncLock = new object();

        static WallpaperManager()
        {
            // Always ensure the user's original wallpaper is restored on process exit
            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreOriginalWallpaper();
        }

        private static void SaveBackupToRegistry(BackupData backup)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                if (key != null)
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(backup);
                    key.SetValue(BackupValueName, json);
                }
            }
            catch { }
        }

        private static BackupData? LoadBackupFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
                if (key != null)
                {
                    string? json = key.GetValue(BackupValueName)?.ToString();
                    if (!string.IsNullOrEmpty(json))
                    {
                        return System.Text.Json.JsonSerializer.Deserialize<BackupData>(json);
                    }
                }
            }
            catch { }
            return null;
        }

        private static void ClearBackupInRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey, true);
                key?.DeleteValue(BackupValueName, false);
            }
            catch { }
        }

        private static string? FindTrueOriginalWallpaperFallback()
        {
            try
            {
                using var wpKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
                if (wpKey != null)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        string? hist = wpKey.GetValue($"BackgroundHistoryPath{i}")?.ToString();
                        if (!string.IsNullOrEmpty(hist) && !hist.Contains("ransom_a90_wallpaper", StringComparison.OrdinalIgnoreCase) && File.Exists(hist))
                        {
                            return hist;
                        }
                    }

                    string? curr = wpKey.GetValue("CurrentWallpaperPath")?.ToString();
                    if (!string.IsNullOrEmpty(curr) && !curr.Contains("ransom_a90_wallpaper", StringComparison.OrdinalIgnoreCase) && File.Exists(curr))
                    {
                        return curr;
                    }
                }
            }
            catch { }

            string[] defaults = {
                @"C:\Windows\Web\Wallpaper\ThemeB\img26.jpg",
                @"C:\Windows\Web\Wallpaper\ThemeB\img25.jpg",
                @"C:\Windows\Web\Wallpaper\ThemeB\img24.jpg",
                @"C:\Windows\Web\Wallpaper\Windows\img19.jpg",
                @"C:\Windows\Web\Wallpaper\Windows\img0.jpg"
            };
            foreach (var d in defaults)
            {
                if (File.Exists(d)) return d;
            }
            return null;
        }

        /// <summary>
        /// Backs up original wallpaper configuration and sets desktop to checkered A-90 + stop icon wallpaper
        /// </summary>
        public static void ApplyRansomWallpaper()
        {
            lock (syncLock)
            {
                if (isRansomWallpaperActive) return;

                try
                {
                    // 1. Read or create backup
                    BackupData backup = inMemoryBackup ?? LoadBackupFromRegistry() ?? new BackupData();

                    if (backup.Monitors.Count == 0)
                    {
                        var dw = GetDesktopWallpaperCOM();
                        if (dw != null)
                        {
                            try
                            {
                                dw.GetMonitorDevicePathCount(out uint count);
                                for (uint i = 0; i < count; i++)
                                {
                                    dw.GetMonitorDevicePathAt(i, out string monId);
                                    dw.GetWallpaper(monId, out string wp);

                                    if (!string.IsNullOrEmpty(wp) && !wp.Contains("ransom_a90_wallpaper", StringComparison.OrdinalIgnoreCase) && File.Exists(wp))
                                    {
                                        backup.Monitors[monId] = wp;
                                    }
                                    else
                                    {
                                        string? fallback = FindTrueOriginalWallpaperFallback();
                                        if (fallback != null) backup.Monitors[monId] = fallback;
                                    }
                                }
                                dw.GetPosition(out int pos);
                                backup.Position = pos;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error querying IDesktopWallpaper: {ex.Message}");
                            }
                        }

                        // Also query legacy registry values
                        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                        {
                            if (key != null)
                            {
                                string? lp = key.GetValue("Wallpaper")?.ToString();
                                if (!string.IsNullOrEmpty(lp) && !lp.Contains("ransom_a90_wallpaper", StringComparison.OrdinalIgnoreCase))
                                {
                                    backup.LegacyPath = lp;
                                }
                                else
                                {
                                    backup.LegacyPath = FindTrueOriginalWallpaperFallback();
                                }
                                backup.LegacyStyle = key.GetValue("WallpaperStyle")?.ToString() ?? "10";
                                backup.LegacyTile = key.GetValue("TileWallpaper")?.ToString() ?? "0";
                            }
                        }

                        SaveBackupToRegistry(backup);
                        inMemoryBackup = backup;
                    }

                    // 2. Generate checkered wallpaper bitmap if not already generated
                    if (string.IsNullOrEmpty(generatedWallpaperPath) || !File.Exists(generatedWallpaperPath))
                    {
                        generatedWallpaperPath = GenerateCheckeredWallpaper();
                    }

                    if (!string.IsNullOrEmpty(generatedWallpaperPath) && File.Exists(generatedWallpaperPath))
                    {
                        // 3. Set multi-monitor wallpaper via IDesktopWallpaper COM
                        var dw = GetDesktopWallpaperCOM();
                        if (dw != null)
                        {
                            try
                            {
                                dw.SetPosition(4); // DWPOS_FILL
                                dw.SetWallpaper(null, generatedWallpaperPath); // null sets all monitors
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error setting IDesktopWallpaper: {ex.Message}");
                            }
                        }

                        // 4. Set legacy registry and broadcast SystemParametersInfo as fallback
                        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                        {
                            if (key != null)
                            {
                                key.SetValue("WallpaperStyle", "10"); // Fill / Span
                                key.SetValue("TileWallpaper", "0");
                            }
                        }
                        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, generatedWallpaperPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                        isRansomWallpaperActive = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying ransom wallpaper: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restores the user's original desktop wallpaper across all monitors
        /// </summary>
        public static void RestoreOriginalWallpaper()
        {
            lock (syncLock)
            {
                try
                {
                    BackupData? backup = inMemoryBackup ?? LoadBackupFromRegistry();
                    string? fallback = FindTrueOriginalWallpaperFallback();

                    // 1. Restore via IDesktopWallpaper COM
                    var dw = GetDesktopWallpaperCOM();
                    if (dw != null)
                    {
                        try
                        {
                            if (backup != null && backup.Monitors.Count > 0)
                            {
                                foreach (var kvp in backup.Monitors)
                                {
                                    string wp = File.Exists(kvp.Value) ? kvp.Value : (fallback ?? kvp.Value);
                                    if (File.Exists(wp))
                                    {
                                        dw.SetWallpaper(kvp.Key, wp);
                                    }
                                }
                                dw.SetPosition(backup.Position);
                            }
                            else if (fallback != null && File.Exists(fallback))
                            {
                                dw.SetWallpaper(null, fallback);
                                dw.SetPosition(4);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error restoring IDesktopWallpaper: {ex.Message}");
                        }
                    }

                    // 2. Restore legacy registry values
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                    {
                        if (key != null)
                        {
                            if (backup?.LegacyStyle != null)
                                key.SetValue("WallpaperStyle", backup.LegacyStyle);
                            if (backup?.LegacyTile != null)
                                key.SetValue("TileWallpaper", backup.LegacyTile);
                        }
                    }

                    // 3. Restore legacy wallpaper image via SystemParametersInfo
                    string? primaryPath = backup?.LegacyPath;
                    if (string.IsNullOrEmpty(primaryPath) || !File.Exists(primaryPath) || primaryPath.Contains("ransom_a90_wallpaper", StringComparison.OrdinalIgnoreCase))
                    {
                        primaryPath = fallback;
                    }

                    if (!string.IsNullOrEmpty(primaryPath) && File.Exists(primaryPath))
                    {
                        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, primaryPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    }

                    ClearBackupInRegistry();
                    inMemoryBackup = null;
                    isRansomWallpaperActive = false;

                    // Clean up temporary generated wallpaper
                    if (!string.IsNullOrEmpty(generatedWallpaperPath) && File.Exists(generatedWallpaperPath))
                    {
                        try { File.Delete(generatedWallpaperPath); } catch { }
                        generatedWallpaperPath = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error restoring wallpaper: {ex.Message}");
                }
            }
        }

        private static Bitmap? cachedWallpaperBitmap = null;

        public static Bitmap GetCheckeredWallpaperBitmap()
        {
            lock (syncLock)
            {
                if (cachedWallpaperBitmap != null) return cachedWallpaperBitmap;

                int w = Math.Max(1920, SystemInformation.VirtualScreen.Width);
                int h = Math.Max(1080, SystemInformation.VirtualScreen.Height);
                int tileSize = 160;

                Bitmap wallpaper = new Bitmap(w, h, PixelFormat.Format32bppRgb);
                using (Bitmap a90 = Properties.Resources.ransom_idle)
                using (Bitmap stop = Properties.Resources.stop_sign)
                using (Graphics g = Graphics.FromImage(wallpaper))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.SmoothingMode = SmoothingMode.None;

                    int cols = (int)Math.Ceiling((double)w / tileSize);
                    int rows = (int)Math.Ceiling((double)h / tileSize);

                    Color darkColor = Color.FromArgb(18, 2, 2);     // Near-black dark blood
                    Color crimsonColor = Color.FromArgb(68, 6, 6);  // Deep textured crimson

                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            int x = c * tileSize;
                            int y = r * tileSize;
                            Rectangle rect = new Rectangle(x, y, tileSize, tileSize);

                            bool isEven = (r + c) % 2 == 0;
                            using (Brush b = new SolidBrush(isEven ? darkColor : crimsonColor))
                            {
                                g.FillRectangle(b, rect);
                            }

                            // Alternate between A-90 face and Stop Sign icon
                            Bitmap icon = isEven ? a90 : stop;
                            int pad = 16;
                            Rectangle destRect = new Rectangle(x + pad, y + pad, tileSize - pad * 2, tileSize - pad * 2);
                            g.DrawImage(icon, destRect);

                            // Subtle dark border between tiles
                            using (Pen p = new Pen(Color.FromArgb(40, 4, 4), 1))
                            {
                                g.DrawRectangle(p, rect);
                            }
                        }
                    }
                }

                cachedWallpaperBitmap = wallpaper;
                return cachedWallpaperBitmap;
            }
        }

        private static string GenerateCheckeredWallpaper()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "ransom_a90_wallpaper.bmp");
            Bitmap wallpaper = GetCheckeredWallpaperBitmap();
            wallpaper.Save(tempPath, ImageFormat.Bmp);
            return tempPath;
        }
    }
}
