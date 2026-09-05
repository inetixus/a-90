namespace rans0m
{
    internal static class Program
    {
        private static KeyboardHook keyboardHook = new KeyboardHook();

        [STAThread]
        static void Main(string[] args)
        {
            // If launched with a coin file argument, hand off to running instance
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string arg = args[0].Trim().Trim('"');
                if (File.Exists(arg) && (arg.EndsWith(".gold", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".pot", StringComparison.OrdinalIgnoreCase)))
                {
                    // Handoff coin to the running game instance with retries
                    for (int retry = 0; retry < 12; retry++)
                    {
                        if (CoinIpcServer.TrySendToRunningInstance(arg, Cursor.Position))
                        {
                            return; // Successfully handed off! Exit immediately.
                        }
                        Thread.Sleep(120);
                    }

                    // Never launch a second game instance when opening a coin file!
                    return;
                }
            }

            // Enforce single instance for the main game to avoid orphan processes competing for IPC & audio
            using var singleInstanceMutex = new Mutex(true, "Rans0m_MainGame_SingleInstance_Mutex", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                return; // Another instance is already running
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