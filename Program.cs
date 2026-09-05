namespace rans0m
{
    internal static class Program
    {
        private static KeyboardHook keyboardHook = new KeyboardHook();

        [STAThread]
        static void Main(string[] args)
        {
            // If launched with a coin file argument, try sending to the running instance first!
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string arg = args[0].Trim().Trim('"');
                if (arg == "--test-thankyou")
                {
                    ApplicationConfiguration.Initialize();
                    Application.Run(new ThankYou());
                    return;
                }

                if (arg == "--dump-frames")
                {
                    ApplicationConfiguration.Initialize();
                    using var form = new ThankYou();
                    string outDir = args.Length > 1 ? args[1] : Path.GetTempPath();
                    Directory.CreateDirectory(outDir);

                    using (var b1 = form.RenderFrame(350))
                        b1.Save(Path.Combine(outDir, "thankyou_phase1_start.png"), System.Drawing.Imaging.ImageFormat.Png);
                    using (var b2 = form.RenderFrame(950))
                        b2.Save(Path.Combine(outDir, "thankyou_phase1_busy.png"), System.Drawing.Imaging.ImageFormat.Png);
                    using (var b3 = form.RenderFrame(1420))
                        b3.Save(Path.Combine(outDir, "thankyou_flash.png"), System.Drawing.Imaging.ImageFormat.Png);
                    using (var b4 = form.RenderFrame(2200))
                        b4.Save(Path.Combine(outDir, "thankyou_phase2_thumbsup.png"), System.Drawing.Imaging.ImageFormat.Png);

                    Console.WriteLine("DUMP_OK: Frames exported to " + outDir);
                    return;
                }

                if (arg == "--dump-exit-frames")
                {
                    ApplicationConfiguration.Initialize();
                    using var overlay = new Overlay();
                    string outDir = args.Length > 1 ? args[1] : Path.GetTempPath();
                    Directory.CreateDirectory(outDir);

                    double[] factors = new double[] { 0.65, 0.38, 0.16, 0.04 };
                    for (int i = 0; i < factors.Length; i++)
                    {
                        var method = typeof(Overlay).GetMethod("GenerateExitFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        using Bitmap bmp = (Bitmap)method!.Invoke(overlay, new object[] { factors[i], 777 + i * 43 })!;
                        string path = Path.Combine(outDir, $"vignette_exit_step_{i + 1}.png");
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    Console.WriteLine("DUMP_OK: Exit frames exported to " + outDir);
                    return;
                }

                if (File.Exists(arg) && (arg.EndsWith(".gold", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".pot", StringComparison.OrdinalIgnoreCase)))
                {
                    if (CoinIpcServer.TrySendToRunningInstance(arg, Cursor.Position))
                    {
                        return; // Successfully handed off to running instance! Exit instantly.
                    }
                }
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => { }; // don't let a random exception kill the whole app

            ApplicationConfiguration.Initialize();

            // Start IPC listener for when user opens coins
            CoinIpcServer.Start((coinPath, clickPos) =>
            {
                // Dispatch to UI thread on Overlay or Ransomed
                Overlay.Instance?.Invoke(() =>
                {
                    CoinFlightOverlay.CollectCoinFromDisk(coinPath, clickPos);
                });
            });

            keyboardHook.KeyPressed += Global.KeyPressed;
            keyboardHook.Hook();

            Application.Run(new Overlay());

            keyboardHook.Unhook();
            CoinIpcServer.Stop();
        }
    }
}