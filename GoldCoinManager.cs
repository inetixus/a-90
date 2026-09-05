using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace rans0m
{

    public class CoinCollectResult
    {
        public bool Success { get; set; }
        public bool IsHoneyPot { get; set; }
        public int Value { get; set; } = 20;

        public static implicit operator bool(CoinCollectResult? r) => r != null && r.Success;
    }

    public static class GoldCoinManager
    {
        private const string RegistryValueName = "GoldCoins"; // REG_MULTI_SZ

        /// <summary>
        /// Creates a collection of .gold coins and .pot honey pots in random user folders.
        /// Each file contains an encrypted JSON object: {"RANSOM_COIN": "randomString", "TYPE": "...", "VALUE": "..."}.
        /// Paths are stored in the registry for later deletion.
        /// </summary>
        public static void CreateRandomCoins(int count)
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Determine item distribution: 2 or 3 Honey Pots (250 coins each), and 16 to 22 normal coins (25-50 each)
            // Generates 1100 - 1500+ total coins so the player always has an abundance of coins to collect!
            int potCount = Global.rng.Next(2, 4); // 2 or 3 Honey Pots
            int coinCount = Global.rng.Next(16, 23); // 16 to 22 Gold Coins
            List<(string fileName, string type, int value)> itemsToCreate = new();

            for (int p = 0; p < potCount; p++)
            {
                itemsToCreate.Add(($"Honey Pot ({p + 1}).pot", "HONEY_POT", 250));
            }

            int currentSum = potCount * 250;
            List<int> coinValues = new();
            for (int c = 0; c < coinCount; c++)
            {
                int val = Global.rng.Next(25, 51); // 25 to 50 coins each
                coinValues.Add(val);
                currentSum += val;
            }

            // Ensure total spawned across all items is at least 1100 (more than double the required 500 ransom)
            while (currentSum < 1100)
            {
                bool bumped = false;
                for (int i = 0; i < coinValues.Count; i++)
                {
                    if (coinValues[i] < 50)
                    {
                        coinValues[i]++;
                        currentSum++;
                        bumped = true;
                        if (currentSum >= 1100) break;
                    }
                }
                if (!bumped)
                {
                    coinValues.Add(35);
                    currentSum += 35;
                }
            }

            for (int c = 0; c < coinValues.Count; c++)
            {
                itemsToCreate.Add(($"Gold Coin ({c + 1}).gold", "COIN", coinValues[c]));
            }

            List<string> createdPaths = new List<string>();

            for (int i = 0; i < itemsToCreate.Count; i++)
            {
                var item = itemsToCreate[i];
                try
                {
                    // Honey Pots ALWAYS placed directly on Desktop for testing and easy access
                    string targetDir;
                    if (item.type == "HONEY_POT" && Directory.Exists(desktopDir))
                    {
                        targetDir = desktopDir;
                    }
                    else if (i < 15 && Directory.Exists(desktopDir))
                    {
                        targetDir = desktopDir;
                    }
                    else if (Directory.Exists(downloadsDir) && Global.rng.Next(2) == 0)
                    {
                        targetDir = downloadsDir;
                    }
                    else
                    {
                        targetDir = Directory.Exists(desktopDir) ? desktopDir : docsDir;
                    }

                    // Generate a unique random string (a GUID)
                    string randomString = Guid.NewGuid().ToString("N"); // 32 hex chars

                    // Build the payload dictionary
                    Dictionary<string, string> payload = new Dictionary<string, string>
                    {
                        { "RANSOM_COIN", randomString },
                        { "TYPE", item.type },
                        { "VALUE", item.value.ToString() }
                    };

                    // Serialize to JSON
                    string json = JsonSerializer.Serialize(payload);
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                    // Encrypt using DPAPI (user‑specific)
                    byte[] encrypted = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);

                    string fullPath = Path.Combine(targetDir, item.fileName);

                    // Write the encrypted data to the file
                    File.WriteAllBytes(fullPath, encrypted);

                    createdPaths.Add(fullPath);
                }
                catch { } // couldn't write there, skip this coin
            }

            // Store all created paths in the registry (append if already exists)
            AppendToRegistryList(createdPaths);
        }

        /// <summary>
        /// Deletes all .gold and .pot files and removes them from the Registry.
        /// </summary>
        public static void DeleteAllCoins()
        {
            List<string>? paths = GetRegistryFileList();
            if (paths != null)
            {
                foreach (string path in paths)
                {
                    try { File.Delete(path); }
                    catch { }
                }
            }

            // Additional safety sweep: clean up any remaining .gold or .pot files from all desktop locations
            try
            {
                var desktopDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string sfDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(sfDesktop)) desktopDirs.Add(sfDesktop);

                string upDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
                if (Directory.Exists(upDesktop)) desktopDirs.Add(upDesktop);

                foreach (string desktop in desktopDirs)
                {
                    foreach (var f in Directory.GetFiles(desktop, "Gold Coin*.gold"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                    foreach (var f in Directory.GetFiles(desktop, "Honey Pot*.pot"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }

                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(downloadsDir))
                {
                    foreach (var f in Directory.GetFiles(downloadsDir, "Gold Coin*.gold")) try { File.Delete(f); } catch { }
                    foreach (var f in Directory.GetFiles(downloadsDir, "Honey Pot*.pot")) try { File.Delete(f); } catch { }
                }

                string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(docsDir))
                {
                    foreach (var f in Directory.GetFiles(docsDir, "Gold Coin*.gold")) try { File.Delete(f); } catch { }
                    foreach (var f in Directory.GetFiles(docsDir, "Honey Pot*.pot")) try { File.Delete(f); } catch { }
                }
            }
            catch { }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\RANSOM"))
            {
                if (key != null)
                    key.DeleteValue(RegistryValueName, false);
            }
        }

        /// <summary>
        /// Decrypts a .gold or .pot file and returns the dictionary inside.
        /// </summary>
        public static Dictionary<string, string>? DecryptCoinFile(string filePath)
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(filePath);
                byte[] decryptedBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decryptedBytes);

                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates a .gold or .pot file, verifies it hasn't been used yet, marks it used, and deletes it.
        /// Returns CoinCollectResult detailing success, item type, and value.
        /// </summary>
        public static CoinCollectResult TryCollectCoin(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new CoinCollectResult { Success = false };

            try
            {
                var goldFileData = DecryptCoinFile(filePath);
                if (goldFileData != null && goldFileData.TryGetValue("RANSOM_COIN", out string? coinId) && !string.IsNullOrEmpty(coinId))
                {
                    lock (Global.usedCoins)
                    {
                        if (Global.usedCoins.Contains(coinId))
                            return new CoinCollectResult { Success = false };

                        Global.usedCoins.Add(coinId);
                    }

                    try 
                    { 
                        File.Delete(filePath); 
                        FileTypeRegister.NotifyShellFileDeleted(filePath);
                    } 
                    catch { }

                    string type = "COIN";
                    if (goldFileData.TryGetValue("TYPE", out string? t) && !string.IsNullOrEmpty(t))
                    {
                        type = t;
                    }
                    else if (filePath.EndsWith(".pot", StringComparison.OrdinalIgnoreCase) || filePath.Contains("Honey Pot", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "HONEY_POT";
                    }

                    int val = (type == "HONEY_POT") ? 250 : 20;
                    if (goldFileData.TryGetValue("VALUE", out string? vStr) && int.TryParse(vStr, out int parsedVal))
                    {
                        val = parsedVal;
                    }

                    return new CoinCollectResult
                    {
                        Success = true,
                        IsHoneyPot = (type == "HONEY_POT"),
                        Value = val
                    };
                }
            }
            catch { }

            return new CoinCollectResult { Success = false };
        }

        // ----------------------------- INTERNAL HELPERS -----------------------------

        /// <summary>
        /// Gets a list of user directories to use as base paths for creating .gold files 
        /// (We only get user directories cause it would be too hard to search in the system directories)
        /// </summary>
        /// <returns></returns>
        private static List<string> GetUserDirectories()
        {
            var dirs = new List<string>();

            Environment.SpecialFolder[] specialFolders =
            {
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.UserProfile
            };

            foreach (Environment.SpecialFolder sf in specialFolders)
            {
                string path = Environment.GetFolderPath(sf);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    dirs.Add(path);
            }

            // Add Downloads folder (Not found in SpecialFolders)
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads))
                dirs.Add(downloads);

            // Remove duplicates
            return dirs.Distinct().ToList();
        }

        private static string GetRandomSubfolder(string root)
        {
            string[] subDirs = Directory.GetDirectories(root);
            if (subDirs.Length == 0)
                return root;

            return subDirs[Global.rng.Next(subDirs.Length)];
        }
        
        private static void AppendToRegistryList(IEnumerable<string> newPaths)
        {
            List<string> existing = GetRegistryFileList() ?? new List<string>();
            existing.AddRange(newPaths);
            SetRegistryFileList(existing);
        }

        private static List<string>? GetRegistryFileList()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\RANSOM"))
            {
                if (key == null) return null;
                object? value = key.GetValue(RegistryValueName);

                if (value is string[] multiString)
                    return new List<string>(multiString);
                return null;
            }
        }

        private static void SetRegistryFileList(List<string> paths)
        {
            using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\RANSOM"))
            {
                if (key == null) return;
                // Convert to array of strings (REG_MULTI_SZ)
                key.SetValue(RegistryValueName, paths.ToArray(), RegistryValueKind.MultiString);
            }
        }
    }
}