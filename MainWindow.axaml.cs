using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CmlLib.Core;
using FireSharp.Core;
using FireSharp.Core.Config;
using FireSharp.Core.Interfaces;
using MsgBox;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModpackUpdater4
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private IFirebaseConfig config = new FirebaseConfig()
        {
            AuthSecret = "0EzQI0hBS9VFfxl1p9MxVY2WshoA8ChhXIapPBgB",
            BasePath = "https://modupdater-afe8b.firebaseio.com/"
        };

        private IFirebaseClient client;

        #region DownloadParameters

        private string ModpackBaseDirectory;
        private string ModpackModsDirectory;
        private string DropboxKey = "";
        private List<string> FilesToDelete = new List<string>();
        private List<string> FilesToDownload = new List<string>();
        private string forgeversion;
        private readonly string ModpackSavePath = Path.Combine(MinecraftPath.GetOSDefaultPath(), "Modpacks_profiles.json");

        #endregion DownloadParameters

        #region UIElements

        private TabControl tab;

        private TextBlock status;
        private TextBox nome;
        private TextBox token;
        private Button configBtn;
        private Button download;
        private ProgressBar bar;

        public ComboBox modpackCBox;
        private TextBox modList;

        private TextBlock link;
        private Avalonia.Controls.Image image;
        private TextBox ForgeU;
        private TextBox ModU;
        private TextBox NomeU;
        private TextBox DropboxU;
        private Button SelectForge;
        private Button SelectMods;
        private Button Upload;
        private ProgressBar UploadPB;
        private TextBlock statusU;

        #endregion UIElements

        /// <summary>
        /// Molto incasinato, in poche parole mi permette di interagire con la GUI dal codice
        /// Inoltre cambio icona all'app
        /// </summary>
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            client = new FirebaseClient(config);

            tab = this.FindControl<TabControl>("tabControl1");
            nome = this.FindControl<TextBox>("NameTxt");
            token = this.FindControl<TextBox>("TokenTxt");
            status = this.FindControl<TextBlock>("StatusTextBlock");
            configBtn = this.FindControl<Button>("ConfigBtn");
            download = this.FindControl<Button>("DownloadBtn");
            bar = this.FindControl<ProgressBar>("DownloadPB");
            modpackCBox = this.FindControl<ComboBox>("ModpackCBox");
            modpackCBox.SelectionChanged += ModpackCBox_SelectionChanged;
            modList = this.FindControl<TextBox>("Modlist");
            link = this.FindControl<TextBlock>("LinkTextBlock");
            link.Foreground = Brushes.Blue;
            link.Tapped += Link_Tapped;
            image = this.FindControl<Avalonia.Controls.Image>("ModpackIcon");
            image.Tapped += Image_Tapped;
            ForgeU = this.FindControl<TextBox>("ForgeTxtU");
            ModU = this.FindControl<TextBox>("ModsTxtU");
            NomeU = this.FindControl<TextBox>("NameTxtU");
            DropboxU = this.FindControl<TextBox>("TokenTxtU");
            SelectForge = this.FindControl<Button>("ForgeBtn");
            SelectMods = this.FindControl<Button>("ModsBtn");
            Upload = this.FindControl<Button>("UploadBtn");
            UploadPB = this.FindControl<ProgressBar>("UploadPB");
            statusU = this.FindControl<TextBlock>("StatusTextBlockU");
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            Bitmap bitmap = new Bitmap(assets.Open(new Uri("resm:ModpackUpdater4.AppIcon.png")));
            WindowIcon icon = new WindowIcon(bitmap);
            this.Icon = icon;
        }

        /// <summary>
        /// Prende i dati di input da un file json
        /// </summary>
        public async void ConfigureFromJson(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                AllowMultiple = false,
                Title = "Seleziona configurazione...",
                Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter(){Name="File Json", Extensions=new List<string>{ "json"} }
                },
                InitialFileName = "config.json",
            };

            string[] results = await dialog.ShowAsync(this);
            if (results.Length == 1)
            {
                if (File.Exists(results[0]))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(results[0]);
                        Config config = JsonConvert.DeserializeObject<Config>(json);

                        nome.Text = config.nome;
                        token.Text = config.token;
                    }
                    catch
                    {
                        await MessageBox.Show(this, "Impossibile leggere il file", "Errore", MessageBox.MessageBoxButtons.Ok);
                    }
                }
                else
                {
                    await MessageBox.Show(this, "Il file non esiste", "Errore", MessageBox.MessageBoxButtons.Ok);
                }
            }
        }

        /// <summary>
        /// Comincia il download del Modpack
        /// </summary>
        public async void DownloadModpack(object sender, RoutedEventArgs e)
        {
            ModDownloader downloader = new ModDownloader();
            LockApp();
            Message("Controllo dei dati...");

            if (token.Text == null && nome.Text == null)
            {
                Stop("I dati non sono validi");
                return;
            }

            Message("Rilevo la cartella del Modpack...");
            if (await downloader.ModpackExists(nome.Text, token.Text))
            {
                try
                {
                    await downloader.Install(this);
                    string ForgeVersion = await Utilities.InstallForge(this, downloader.ModpackBaseDirectory, downloader.modpack.Token);
                    await Utilities.ParseProfile(this, downloader.ModpackBaseDirectory, downloader.modpack.Token, downloader.modpack.Name, ForgeVersion);
                    await ModManager.AddModpack(this, downloader.modpack.Name, token.Text);
                    await MessageBox.Show(this, "Download completato", "Successo", MessageBox.MessageBoxButtons.Ok);
                    downloader.Dispose();
                }
                finally
                {
                    UnlockApp();
                    Message("Fermo");
                }
            }
            else
            {
                Message("Fermo");
                UnlockApp();
                await MessageBox.Show(this, "I dati non sono validi", "Errore", MessageBox.MessageBoxButtons.Ok);
            }
        }

        #region GUI

        /// <summary>
        /// Blocca input da parte dell'utente
        /// </summary>
        public void LockApp()
        {
            tab.IsEnabled = false;
            nome.IsReadOnly = true;
            token.IsReadOnly = true;
            configBtn.IsEnabled = false;
            download.IsEnabled = false;
            bar.IsIndeterminate = true;
            tab.IsEnabled = false;
            SelectForge.IsEnabled = false;
            SelectMods.IsEnabled = false;
            Upload.IsEnabled = false;
            NomeU.IsReadOnly = true;
            DropboxU.IsReadOnly = true;
            ForgeU.IsReadOnly = true;
            ModU.IsReadOnly = true;
            UploadPB.IsIndeterminate = true;
        }

        /// <summary>
        /// Ripristina la possibilita di immettere input
        /// </summary>
        public void UnlockApp()
        {
            tab.IsEnabled = true;
            bar.IsIndeterminate = false;
            nome.IsReadOnly = false;
            token.IsReadOnly = false;
            configBtn.IsEnabled = true;
            download.IsEnabled = true;
            UploadPB.IsIndeterminate = false;
            isUploading = false;
            SelectForge.IsEnabled = true;
            SelectMods.IsEnabled = true;
            Upload.IsEnabled = true;
            NomeU.IsReadOnly = false;
            DropboxU.IsReadOnly = false;
            ForgeU.IsReadOnly = false;
            ModU.IsReadOnly = false;
        }

        /// <summary>
        /// Imposta come stato dell'app un messaggio
        /// </summary>
        /// <param name="message"></param>
        public void Message(string message)
        {
            status.Text = message;
            statusU.Text = message;
        }

        public async void Stop(string message)
        {
            Message("Fermo");
            await MessageBox.Show(this, message, "Errore", MessageBox.MessageBoxButtons.Ok);
            UnlockApp();
        }

        #endregion GUI

        #region ManageModpack

        private bool First = true;

        /// <summary>
        /// Ogni volta che si cambia scheda il aggiorna la lista di modpack
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void Tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (First)
            {
                First = false;
            }
            else
            {
                if (this.FindControl<TabItem>("tabPage3").IsSelected)
                {
                    if (File.Exists(ModpackSavePath))
                    {
                        Modpacks modpacks = JsonConvert.DeserializeObject<Modpacks>(await File.ReadAllTextAsync(ModpackSavePath));
                        List<string> items = new List<string>();
                        foreach (KeyValuePair<string, Modpack> modpack in modpacks.modpack)
                        {
                            items.Add(modpack.Value.Name);
                        }
                        modpackCBox.Items = items;
                    }
                }
            }
        }

        /// <summary>
        /// Cambia la lista di mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ModpackCBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            modList.Text = "";
            if (modpackCBox.SelectedItem != null)
            {
                if (File.Exists(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", (string)modpackCBox.SelectedItem, "Files.txt")))
                {
                    foreach (string str in await File.ReadAllLinesAsync(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", (string)modpackCBox.SelectedItem, "Files.txt")))
                    {
                        modList.Text += $"{str.Replace(".jar", "")}\n";
                    }
                }
                else
                {
                    foreach (FileInfo file in new DirectoryInfo(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", (string)modpackCBox.SelectedItem, "mods")).GetFiles())
                    {
                        await File.AppendAllTextAsync(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", (string)modpackCBox.SelectedItem, "Files.txt"), file.Name + Environment.NewLine);
                    }

                    foreach (string str in await File.ReadAllLinesAsync(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", (string)modpackCBox.SelectedItem, "Files.txt")))
                    {
                        modList.Text += $"{str.Replace(".jar", "")}\n";
                    }
                }
            }
        }

        /// <summary>
        /// Rimuove un modpack
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void UninstallBtn(object sender, RoutedEventArgs e)
        {
            if (await MessageBox.Show(this, "Vuoi davvero disinstallare il modpack?", "Sei sicuro?", MessageBox.MessageBoxButtons.YesNo) == MessageBox.MessageBoxResult.Yes)
            {
                await ModManager.RemoveModpack(this, (string)modpackCBox.SelectedItem);
            }
        }

        /// <summary>
        /// Aggiorna un modpack
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void CheckUpdatesBtn(object sender, RoutedEventArgs e)
        {
            Modpacks modpacks = JsonConvert.DeserializeObject<Modpacks>(await File.ReadAllTextAsync(ModpackSavePath));
            foreach (KeyValuePair<string, Modpack> str in modpacks.modpack)
            {
                if (str.Value.Name == (string)modpackCBox.SelectedItem)
                {
                    nome.Text = str.Value.Name;
                    token.Text = str.Value.Token;
                    tab.SelectedItem = this.FindControl<TabItem>("tabPage1");
                    DownloadModpack(sender, e);
                }
            }
        }

        #endregion ManageModpack

        #region CreateModpack

        private bool isUploading = false;
        private string IconPath = Path.Combine(Path.GetTempPath(), "ModpackIcon.png");
        private bool IconSelected = false;

        /// <summary>
        /// Link a un tutorial su come creare una chiave api di dropbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Link_Tapped(object sender, RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://youtu.be/ZnklM6HxDss",
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        /// <summary>
        /// Openfiledialog per l'icona del modpack, poi elaborata ridurre la risoluzione
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Image_Tapped(object sender, RoutedEventArgs e)
        {
            if (isUploading)
                return;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                AllowMultiple = false,
                Title = "Seleziona immagine...",
                Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter(){Name="File Png", Extensions=new List<string>{ "png"} }
                },
                InitialFileName = "icon.png",
            };

            string[] results = await dialog.ShowAsync(this);
            if (results.Length == 1)
            {
                if (File.Exists(results[0]))
                {
                    if (File.Exists(IconPath))
                    {
                        File.Delete(IconPath);
                    }
                    using (SixLabors.ImageSharp.Image image = await SixLabors.ImageSharp.Image.LoadAsync(results[0]))
                    {
                        image.Mutate(x => x.Resize(128, 128));
                        image.Save(Path.Combine(Path.GetTempPath(), "ModpackIcon.png"));
                        IconSelected = true;
                    }
                    image.Source = new Bitmap(IconPath);
                }
            }
        }

        /// <summary>
        /// OpenFileDialog per selezionare la versione di forge
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void SelectForgePath(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                AllowMultiple = false,
                Title = "Seleziona Forge...",
                Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter(){Name="File jar", Extensions=new List<string>{ "jar"} }
                },
                InitialFileName = "forge.jar",
            };

            string[] results = await dialog.ShowAsync(this);
            if (results.Length == 1)
            {
                if (File.Exists(results[0]))
                {
                    ForgeU.Text = results[0];
                }
            }
        }

        /// <summary>
        /// OpenFolderDialog per selezionare le mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void SelectModPath(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog()
            {
                Directory = MinecraftPath.GetOSDefaultPath(),
                Title = "Selezione cartella mod"
            };

            string path = await dialog.ShowAsync(this);
            if (Directory.Exists(path))
            {
                ModU.Text = path;
            }
        }

        /// <summary>
        /// Controlla se l'input fornito è valido
        /// </summary>
        /// <param name="sende"></param>
        /// <param name="e"></param>
        public async void ValidateInfo(object sende, RoutedEventArgs e)
        {
            isUploading = true;
            Message("Controllo dei dati...");

            bool Invalid = false;
            if (string.IsNullOrEmpty(NomeU.Text))
            {
                Invalid = true;
            }
            else if (NomeU.Text.Contains("*") || NomeU.Text.Contains(".") || NomeU.Text.Contains("\"") || NomeU.Text.Contains("/") || NomeU.Text.Contains("[") || NomeU.Text.Contains("]") || NomeU.Text.Contains(":") || NomeU.Text.Contains(";") || NomeU.Text.Contains("|") || NomeU.Text.Contains(","))
            {
                Invalid = true;
            }

            if (string.IsNullOrEmpty(DropboxU.Text))
            {
                Invalid = true;
            }
            else if (!await Utilities.isDropboxTokenValid(DropboxU.Text))
            {
                UnlockApp();
                isUploading = false;
                await MessageBox.Show(this, "Token non valido", "Errore", MessageBox.MessageBoxButtons.Ok);

                return;
            }

            if (string.IsNullOrEmpty(ForgeU.Text))
            {
                Invalid = true;
            }
            else if (!File.Exists(ForgeU.Text))
            {
                Invalid = true;
            }

            if (string.IsNullOrEmpty(ModU.Text))
            {
                Invalid = true;
            }
            else if (!Directory.EnumerateFiles(ModU.Text).Any())
            {
                UnlockApp();
                isUploading = false;
                await MessageBox.Show(this, "Cartella mod vuota", "Errore", MessageBox.MessageBoxButtons.Ok);
                return;
            }
            else if (!Directory.Exists(ModU.Text))
            {
                Invalid = true;
            }

            if (!Invalid)
            {
                ModUploader uploader = new ModUploader();
                string NewToken = await uploader.BeginUpload(this, NomeU.Text, DropboxU.Text, ForgeU.Text, ModU.Text, IconSelected);

                await DisplayResults(NewToken);
            }
            else
            {
                UnlockApp();
                isUploading = false;
                await MessageBox.Show(this, "Parametri non validi", "Errore", MessageBox.MessageBoxButtons.Ok);
            }
        }

        public async Task DisplayResults(string NewToken)
        {
            if (await MessageBox.Show(this, "Vuoi installare il modpack nel launcher?", "Upload Completato", MessageBox.MessageBoxButtons.YesNo) == MessageBox.MessageBoxResult.Yes)
            {
                await InstallModpack(NewToken);
            }

            if (await MessageBox.Show(this, "Vuoi creare il file per l'installazione pre-configurata?", "Upload Completato", MessageBox.MessageBoxButtons.YesNo) == MessageBox.MessageBoxResult.Yes)
            {
                Config config = new Config()
                {
                    nome = NomeU.Text,
                    token = NewToken
                };

                SaveFileDialog dialog = new SaveFileDialog()
                {
                    Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Title = "Salva configurazione...",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter(){Name="File Json", Extensions=new List<string>{ "json"} }
                    },
                };

                string configPath = await dialog.ShowAsync(this);
                await File.WriteAllTextAsync(configPath, JsonConvert.SerializeObject(config));
            }

            await MessageBox.Show(this, $"Nome={NomeU.Text}, Token={NewToken}", "Upload Completato", MessageBox.MessageBoxButtons.Ok);
            UnlockApp();
            statusU.Text = "Fermo";
            return;
        }

        /// <summary>
        /// Installa Il Modpack Nel launcher di minecraft e nel modpackUpdater
        /// </summary>
        /// <returns></returns>
        private async Task InstallModpack(string token)
        {
            ModpackBaseDirectory = Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", NomeU.Text);
            ModpackModsDirectory = Path.Combine(ModpackBaseDirectory, "mods");

            Message("Creazione file necessari...");

            if (!Directory.Exists(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances")))
                Directory.CreateDirectory(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances"));
            if (!Directory.Exists(ModpackBaseDirectory))
                Directory.CreateDirectory(ModpackBaseDirectory);
            if (!Directory.Exists(ModpackModsDirectory))
            {
                Directory.CreateDirectory(ModpackModsDirectory);
            }
            else
            {
                Directory.Delete(ModpackModsDirectory, true);
                Directory.CreateDirectory(ModpackModsDirectory);
            }

            foreach (FileInfo file in new DirectoryInfo(ModU.Text).GetFiles())
            {
                statusU.Text = $"Copia del file: {file.Name}";
                file.CopyTo(Path.Combine(ModpackModsDirectory, file.Name));
            }

            string forgeVersion = await Utilities.InstallForge(this, ModpackBaseDirectory, DropboxU.Text);

            await Utilities.ParseProfile(this, ModpackBaseDirectory, DropboxU.Text, NomeU.Text, forgeVersion);

            await ModManager.AddModpack(this, NomeU.Text, token);
        }

        #endregion CreateModpack
    }
}