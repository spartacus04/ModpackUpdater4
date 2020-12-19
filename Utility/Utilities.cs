using Avalonia;
using Avalonia.Platform;
using CmlLib.Core;
using Dropbox.Api;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FireSharp.Core.Config;

namespace ModpackUpdater4
{
    internal static class Utilities
    {
        /// <summary>
        /// Verifica se il token di dropbox è valido
        /// </summary>
        /// <param name="token">Token di dropbox</param>
        /// <returns>Restituisce un valore booleano indicante se il token di dropbox è valido</returns>
        static public async Task<bool> isDropboxTokenValid(string token)
        {
            try
            {
                DropboxClient client = new DropboxClient(token);
                await client.Files.ListFolderAsync(string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Controlla se la cartella Minecraft esiste
        /// </summary>
        /// <returns>Restituisce un valore booleano indicante se la cartella Minecraft esiste</returns>
        static public bool doesMinecraftExist()
        {
            if (Directory.Exists(MinecraftPath.GetOSDefaultPath()))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica se il modpack era gia stato installato
        /// </summary>
        /// <param name="ModpackBaseDirectory"></param>
        /// <param name="ModpackModsDirectory"></param>
        /// <returns>Restituisce un valore booleano indicante se il modpack era gia stato installato</returns>
        static public bool isModpackInstalled(string ModpackBaseDirectory, string ModpackModsDirectory)
        {
            if (Directory.Exists(ModpackBaseDirectory))
            {
                if (!Directory.Exists(ModpackModsDirectory))
                    Directory.CreateDirectory(ModpackModsDirectory);
                return true;
            }
            else
            {
                if (!Directory.Exists(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances")))
                    Directory.CreateDirectory(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances"));

                if (!Directory.Exists(ModpackBaseDirectory))
                    Directory.CreateDirectory(ModpackBaseDirectory);
                if (!Directory.Exists(ModpackModsDirectory))
                    Directory.CreateDirectory(ModpackModsDirectory);
                return false;
            }
        }

        /// <summary>
        /// Installa Forge e rileva la sua versione
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ModpackBaseDirectory">Directory del modpack</param>
        /// <param name="DropboxKey">token di DropBox</param>
        /// <returns>Restituisce la versione di forge installata</returns>
        static public async Task<string> InstallForge(MainWindow sender, string ModpackBaseDirectory, string DropboxKey)
        {
            string forgePath = Path.Combine(ModpackBaseDirectory, "forge.jar");

            sender.Message("Download di Forge installer...");
            if (File.Exists(forgePath))
            {
                File.Delete(forgePath);
            }
            using (var dbx = new DropboxClient(DropboxKey))
            {
                using (var response = await dbx.Files.DownloadAsync(@"/forge.jar"))
                {
                    using (var fileStream = File.Create(forgePath))
                    {
                        (await response.GetContentAsStreamAsync()).CopyTo(fileStream);
                    }
                }
            }
            sender.Message("Installazione di Forge...");
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo(forgePath) { UseShellExecute = true }
            };
            process.Start();
            process.WaitForExit();

            sender.Message("Rilevo la versione di Forge...");

            ZipFile.ExtractToDirectory(forgePath, Path.Combine(ModpackBaseDirectory, "ForgeDecompilation"), true);
            string[] parsedJson = await File.ReadAllLinesAsync(Path.Combine(ModpackBaseDirectory, "ForgeDecompilation", "version.json"));
            string json = "";
            foreach (string str in parsedJson) { json += str; }
            dynamic data = JsonConvert.DeserializeObject(json);
            Directory.Delete(Path.Combine(ModpackBaseDirectory, "ForgeDecompilation"), true);
            return data.id;
        }

        /// <summary>
        /// Crea un nuovo profilo nel launcher di minecraft impostato con il 75% della ram del sistema e un icona personalizzata
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ModpackBaseDirectory">Directory del modpack</param>
        /// <param name="DropboxKey">token di DropBox</param>
        /// <param name="nome">Nome del Modpack</param>
        /// <param name="forgeversion">Versione di forge</param>
        /// <returns></returns>
        static public async Task ParseProfile(MainWindow sender, string ModpackBaseDirectory, string DropboxKey, string nome, string forgeversion)
        {
            sender.Message("Rilevo la quantità di ram nel sistema...");
            float ram = (float)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024f / 1024f / 1024f;
            float allocatedram = (float)Math.Ceiling(ram / 100f * 75f);
            sender.Message("Creo il nuovo profilo nel launcher...");

            string iconPath = Path.Combine(ModpackBaseDirectory, "icon.png");

            using (var dbx = new DropboxClient(DropboxKey))
            {
                using (var response = await dbx.Files.DownloadAsync(@"/icon.png"))
                {
                    using (var fileStream = File.Create(iconPath))
                    {
                        (await response.GetContentAsStreamAsync()).CopyTo(fileStream);
                    }
                }
            }

            byte[] imageArray = File.ReadAllBytes(iconPath);
            string base64ImageRepresentation = Convert.ToBase64String(imageArray);

            string parsedJson = await File.ReadAllTextAsync(Path.Combine(MinecraftPath.GetOSDefaultPath(), "launcher_profiles.json"));
            LauncherProfile root = JsonConvert.DeserializeObject<LauncherProfile>(parsedJson);

            if (root.profiles.ContainsKey("forge"))
            {
                root.profiles.Remove("forge");
            }
            if (!root.profiles.ContainsKey(nome))
            {
                root.profiles.Add(nome, new Profile
                {
                    name = nome,
                    type = "custom",
                    lastVersionId = forgeversion,
                    icon = "data:image/png;base64," + base64ImageRepresentation,
                    gameDir = ModpackBaseDirectory,
                    javaArgs = $@"-Xmx{allocatedram}G -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M",
                });
            }
            await File.WriteAllTextAsync(Path.Combine(MinecraftPath.GetOSDefaultPath(), "launcher_profiles.json"), JsonConvert.SerializeObject(root, Formatting.Indented));
        }

        /// <summary>
        /// Crea un token casuale
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        static public string GenerateToken(int length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            StringBuilder res = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];

                while (length-- > 0)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    res.Append(valid[(int)(num % (uint)valid.Length)]);
                }
            }

            return res.ToString();
        }

        static public FirebaseConfig Credentials()
        {
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();

            StreamReader sr = new StreamReader(assets.Open(new Uri("resm:ModpackUpdater4.FirebaseCredentials.json")));

            string json = sr.ReadToEnd();
            return JsonConvert.DeserializeObject<FirebaseConfig>(json);

        }
    }
}