using CmlLib.Core;
using Dropbox.Api;
using FireSharp.Core;
using FireSharp.Core.Config;
using FireSharp.Core.Interfaces;
using FireSharp.Core.Response;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModpackUpdater4
{
    internal class ModDownloader : IDisposable
    {
        protected Modpacks ModpacksList;
        public Modpack modpack;
        public string ModpackBaseDirectory;
        public string ModpackModsDirectory;

        private IFirebaseConfig config = new FirebaseConfig()
        {
            AuthSecret = "0EzQI0hBS9VFfxl1p9MxVY2WshoA8ChhXIapPBgB",
            BasePath = "https://modupdater-afe8b.firebaseio.com/"
        };

        private IFirebaseClient client;

        public ModDownloader()
        {
            client = new FirebaseClient(config);
        }

        /// <summary>
        /// Controlla se il modpack esiste e prepara per il download del modpack
        /// </summary>
        /// <param name="nome">nome del modpack</param>
        /// <param name="Firebasetoken"></param>
        /// <returns></returns>
        public async Task<bool> ModpackExists(string nome, string Firebasetoken)
        {
            FirebaseResponse response = await client.GetAsync("/");
            ModpacksList = response.ResultAs<Modpacks>();

            if (ModpacksList.modpack.ContainsKey(Firebasetoken))
            {
                modpack = ModpacksList.modpack[Firebasetoken];
                if (await Utilities.isDropboxTokenValid(modpack.Token))
                {
                    if (nome != "")
                    {
                        if (nome.Contains("*") || nome.Contains(".") || nome.Contains("\"") || nome.Contains("/") || nome.Contains("[") || nome.Contains("]") || nome.Contains(":") || nome.Contains(";") || nome.Contains("|") || nome.Contains(","))
                        {
                            return false;
                        }
                        else
                        {
                            ModpackBaseDirectory = Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", nome);
                            ModpackModsDirectory = Path.Combine(ModpackBaseDirectory, "mods");
                            modpack.Name = nome;
                            return true;
                        }
                    }
                    else
                    {
                        ModpackBaseDirectory = Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", nome);
                        ModpackModsDirectory = Path.Combine(ModpackBaseDirectory, "mods");
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Metodo principale, installa il modpack
        /// </summary>
        /// <param name="sender"></param>
        public async Task Install(MainWindow sender)
        {
            if (Utilities.doesMinecraftExist())
            {
                if (Utilities.isModpackInstalled(ModpackBaseDirectory, ModpackModsDirectory))
                {
                    List<string> OnlineMods = await GetServerFiles(sender);
                    List<string> LocalMods = GetLocalFiles(sender);

                    sender.Message("Comparo le versioni...");

                    List<string> ModsToDownload = OnlineMods.Except(LocalMods).ToList();
                    List<string> ModsToDelete = LocalMods.Except(OnlineMods).ToList();

                    await DownloadMods(sender, ModsToDownload, ModsToDelete);
                }
                else
                {
                    List<string> ModsToDownload = await GetServerFiles(sender);

                    await DownloadMods(sender, ModsToDownload);
                }
            }
            else
            {
                sender.Stop("La cartella Minecraft non esiste");
                Dispose();
            }
        }

        /// <summary>
        /// Riceve tutti i nomi delle mod dal server
        /// </summary>
        /// <param name="sender"></param>
        /// <returns>restituisce una lista con i nomi delle mod</returns>
        private async Task<List<string>> GetServerFiles(MainWindow sender)
        {
            List<string> servermods = new List<string>();
            using (var dbx = new DropboxClient(modpack.Token))
            {
                var list = await dbx.Files.ListFolderAsync(string.Empty);

                foreach (var item in list.Entries.Where(i => i.IsFile))
                {
                    if (item.Name != "forge.jar")
                    {
                        if (item.Name != "icon.png")
                        {
                            sender.Message($"Controllo mod server: {item.Name}");
                            servermods.Add(item.Name);
                        }
                    }
                }
            }

            return servermods;
        }

        /// <summary>
        /// Riceve tutti i nomi delle mod installate
        /// </summary>
        /// <param name="sender"></param>
        /// <returns>Restituisce una lista con i nomi delle mod</returns>
        private List<string> GetLocalFiles(MainWindow sender)
        {
            List<string> localmods = new List<string>();
            foreach (FileInfo file in new DirectoryInfo(ModpackModsDirectory).GetFiles())
            {
                sender.Message($"Controllo versione mod locale: {file.Name}");
                localmods.Add(file.Name);
            }

            return localmods;
        }

        /// <summary>
        /// Scarica le mod nuove e rimuove quelle obsolete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="FilesToDownload">Mod da scaricare</param>
        /// <param name="FilesToRemove">Mod da rimuovere</param>
        private async Task DownloadMods(MainWindow sender, List<string> FilesToDownload, List<string> FilesToRemove = null)
        {
            if (FilesToRemove != null)
            {
                foreach (FileInfo file in new DirectoryInfo(ModpackModsDirectory).GetFiles())
                {
                    foreach (string FileToRemove in FilesToRemove)
                    {
                        if (file.Name == FileToRemove)
                        {
                            sender.Message($"Cancellazione dei file non necessari: {file.Name}");
                            file.Delete();
                        }
                    }
                }
            }

            using (var dbx = new DropboxClient(modpack.Token))
            {
                foreach (string file in FilesToDownload)
                {
                    sender.Message($"Download dei file: {file}");
                    using (var response = await dbx.Files.DownloadAsync(@"/" + file))
                    {
                        using (var fileStream = File.Create(Path.Combine(ModpackModsDirectory, file)))
                        {
                            (await response.GetContentAsStreamAsync()).CopyTo(fileStream);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rilascia tutte le risorse usate dalla classe
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}