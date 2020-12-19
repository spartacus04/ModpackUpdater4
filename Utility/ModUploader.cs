using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dropbox.Api;
using Dropbox.Api.Files;
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
    internal class ModUploader : IDisposable
    {
        private IFirebaseConfig config = new FirebaseConfig()
        {
            AuthSecret = "0EzQI0hBS9VFfxl1p9MxVY2WshoA8ChhXIapPBgB",
            BasePath = "https://modupdater-afe8b.firebaseio.com/"
        };

        private IFirebaseClient client;

        public string IconPath = Path.Combine(Path.GetTempPath(), "ModpackIcon.png");

        public ModUploader()
        {
            client = new FirebaseClient(config);
        }

        public async Task<string> BeginUpload(MainWindow sender, string name, string token, string ForgePath, string ModPath, bool IconSelected = false)
        {
            List<string> LocalFiles = GetLocalFiles(sender, name);
            List<string> OnlineFiles = await GetOnlineFiles(sender, token);

            sender.Message("Comparo le versioni...");

            List<string> ModUpload = LocalFiles.Except(OnlineFiles).ToList();
            List<string> FilesToDelete = OnlineFiles.Except(LocalFiles).ToList();
            List<string> FilesToKeep = OnlineFiles.Except(ModUpload).ToList();
            List<string> FilesToUpload = LocalFiles.Except(FilesToKeep).ToList();

            await UploadFiles(sender, FilesToDelete, FilesToUpload, token, ModPath, ForgePath, IconSelected);
            return await CreateCredentials(sender, token, name);
        }

        /// <summary>
        /// Ottiene una lista di file nella cartella specificata
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ModPath"></param>
        /// <returns></returns>
        public List<string> GetLocalFiles(MainWindow sender, string ModPath)
        {
            List<string> LocalFiles = new List<string>();
            foreach (FileInfo file in new DirectoryInfo(ModPath).GetFiles())
            {
                sender.Message($"Controllo versione mod locale: {file.Name}");
                LocalFiles.Add(file.Name);
            }

            return LocalFiles;
        }

        /// <summary>
        /// Ottiene una lista di file dal server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<List<string>> GetOnlineFiles(MainWindow sender, string token)
        {
            List<string> OnlineFiles = new List<string>();
            using (var dbx = new DropboxClient(token))
            {
                var list = await dbx.Files.ListFolderAsync(string.Empty);

                foreach (var item in list.Entries.Where(i => i.IsFile))
                {
                    if (item.Name != "forge.jar")
                    {
                        if (item.Name != "icon.png")
                        {
                            sender.Message($"Controllo versione mod server: {item.Name}");
                            OnlineFiles.Add(item.Name);
                        }
                    }
                }
            }
            return OnlineFiles;
        }

        /// <summary>
        /// Carica sul server le mod, forge e l'icona(se selezionata)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="FilesToDelete">File da cancellare</param>
        /// <param name="FilesToUpload">File da Caricare</param>
        /// <param name="token">Token di dropbox</param>
        /// <param name="ModPath">Percorso alla cartella delle mod</param>
        /// <param name="ForgePath">Percorso al file di forge</param>
        /// <param name="IconSelected"></param>
        public async Task UploadFiles(MainWindow sender, List<string> FilesToDelete, List<string> FilesToUpload, string token, string ModPath, string ForgePath, bool IconSelected)
        {
            if (FilesToDelete.Count > 0)
            {
                using (var dbx = new DropboxClient(token))
                {
                    var list = await dbx.Files.ListFolderAsync(string.Empty);

                    foreach (var item in list.Entries.Where(i => i.IsFile))
                    {
                        if (FilesToDelete.Contains(item.Name))
                        {
                            sender.Message($"Cancello il file deprecato: {item.Name}");
                            await dbx.Files.DeleteV2Async(@"/" + item.Name);
                        }
                    }
                }
            }

            if (FilesToUpload.Count > 0)
            {
                using (var dbx = new DropboxClient(token))
                {
                    const int chunkSize = 128 * 1024;
                    foreach (string mod in FilesToUpload)
                    {
                        using (FileStream stream = new FileStream(Path.Combine(ModPath, mod), FileMode.Open))
                        {
                            sender.Message($"Carico la mod: {mod}");
                            if (stream.Length >= chunkSize)
                            {
                                await stream.DisposeAsync();
                                await ChunkUpload(dbx, Path.Combine(ModPath, mod), mod);
                            }
                            else
                            {
                                await dbx.Files.UploadAsync(@"/" + mod, body: stream);
                            }
                        }
                    }
                }
            }

            sender.Message($"Caricamento di Forge installer...");
            using (var dbx = new DropboxClient(token))
            {
                const int chunkSize = 128 * 1024;
                var list = await dbx.Files.ListFolderAsync(string.Empty);
                foreach (var item in list.Entries.Where(i => i.IsFile))
                {
                    if (item.Name == "forge.jar")
                    {
                        await dbx.Files.DeleteV2Async(@"/" + item.Name);
                    }
                }
                using (FileStream stream = new FileStream(ForgePath, FileMode.Open))
                {
                    if (stream.Length >= chunkSize)
                    {
                        await stream.DisposeAsync();
                        await ChunkUpload(dbx, ForgePath, "forge.jar");
                    }
                    else
                    {
                        await dbx.Files.UploadAsync(@"/" + "forge.jar", body: stream);
                    }
                }
            }

            sender.Message($"Caricamento dell'icona...");
            using (var dbx = new DropboxClient(token))
            {
                const int chunkSize = 128 * 1024;
                var list = await dbx.Files.ListFolderAsync(string.Empty);

                foreach (var item in list.Entries.Where(i => i.IsFile))
                {
                    if (item.Name == "icon.png")
                    {
                        await dbx.Files.DeleteV2Async(@"/" + item.Name);
                    }
                }
                if (IconSelected)
                {
                    using (FileStream stream = new FileStream(IconPath, FileMode.Open))
                    {
                        if (stream.Length >= chunkSize)
                        {
                            await stream.DisposeAsync();
                            await ChunkUpload(dbx, IconPath, "icon.png");
                        }
                        else
                        {
                            await dbx.Files.UploadAsync(@"/" + "icon.png", body: stream);
                        }
                    }
                }
                else
                {
                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    Bitmap bitmap = new Bitmap(assets.Open(new Uri("resm:ModpackUpdater4.icon.png")));
                    if (File.Exists(Path.Combine(Path.GetTempPath(), "TempIcon.bmp")))
                        File.Delete(Path.Combine(Path.GetTempPath(), "TempIcon.bmp"));
                    bitmap.Save(Path.Combine(Path.GetTempPath(), "TempIcon.bmp"));
                    System.Drawing.Bitmap bm = new System.Drawing.Bitmap(Path.Combine(Path.GetTempPath(), "TempIcon.bmp"));
                    bm.Save(IconPath, System.Drawing.Imaging.ImageFormat.Png);
                    using (FileStream stream = new FileStream(IconPath, FileMode.Open))
                    {
                        if (stream.Length >= chunkSize)
                        {
                            await ChunkUpload(dbx, IconPath, "icon.png");
                        }
                        else
                        {
                            await dbx.Files.UploadAsync(@"/" + "icon.png", body: stream);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Crea un nuovo token e lo segna sul server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="token"></param>
        /// <param name="nome"></param>
        /// <returns></returns>
        public async Task<string> CreateCredentials(MainWindow sender, string token, string nome)
        {
            string NewToken = "";
            sender.Message($"Creazione del Token...");

            FirebaseResponse response = await client.GetAsync("/");
            Modpacks modpacks = response.ResultAs<Modpacks>();

            bool alreadyExist()
            {
                foreach (KeyValuePair<string, Modpack> mod in modpacks.modpack)
                {
                    if (mod.Value.Token == token)
                    {
                        NewToken = mod.Key;
                        return true;
                    }
                }
                return false;
            }

            if (!alreadyExist())
            {
                NewToken = Utilities.GenerateToken(7);
                modpacks.modpack.Add(NewToken, new Modpack() { Name = nome, Token = token });
                SetResponse setResponse = await client.SetAsync("/", modpacks);
            }

            return NewToken;
        }

        /// <summary>
        /// Upload delle mod online
        /// </summary>
        /// <param name="client"></param>
        /// <param name="mod"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task ChunkUpload(DropboxClient client, string mod, string fileName)
        {
            const int chunkSize = 128 * 1024;

            byte[] fileContent = await File.ReadAllBytesAsync(mod);

            using (var stream = new MemoryStream(fileContent))
            {
                int numChunks = (int)Math.Ceiling((double)stream.Length / chunkSize);

                byte[] buffer = new byte[chunkSize];
                string sessionId = null;

                for (var idx = 0; idx < numChunks; idx++)
                {
                    var byteRead = stream.Read(buffer, 0, chunkSize);

                    using (MemoryStream memStream = new MemoryStream(buffer, 0, byteRead))
                    {
                        if (idx == 0)
                        {
                            var result = await client.Files.UploadSessionStartAsync(body: memStream);
                            sessionId = result.SessionId;
                        }
                        else
                        {
                            UploadSessionCursor cursor = new UploadSessionCursor(sessionId, (ulong)(chunkSize * idx));

                            if (idx == numChunks - 1)
                            {
                                await client.Files.UploadSessionFinishAsync(cursor, new CommitInfo(@"/" + fileName), memStream);
                            }
                            else
                            {
                                await client.Files.UploadSessionAppendV2Async(cursor, body: memStream);
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}