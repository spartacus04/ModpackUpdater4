using CmlLib.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ModpackUpdater4
{
    internal static class ModManager
    {
        static public readonly string ModpackSavePath = Path.Combine(MinecraftPath.GetOSDefaultPath(), "Modpacks_profiles.json");

        static public async Task AddModpack(MainWindow sender, string nome, string token)
        {
            sender.Message("Creo file di salvataggio del Modpack...");

            if (File.Exists(ModpackSavePath))
            {
                Modpacks modpacks = JsonConvert.DeserializeObject<Modpacks>(await File.ReadAllTextAsync(ModpackSavePath));
                if (!modpacks.modpack.ContainsKey(nome))
                {
                    modpacks.modpack.Add(nome, new Modpack() { Name = nome, Token = token });
                    File.Delete(ModpackSavePath);
                    await File.WriteAllTextAsync(ModpackSavePath, JsonConvert.SerializeObject(modpacks, Formatting.Indented));
                }
            }
            else
            {
                Modpacks modpacks = new Modpacks();
                modpacks.modpack = new Dictionary<string, Modpack>();
                modpacks.modpack.Add(nome, new Modpack() { Name = nome, Token = token });
                await File.WriteAllTextAsync(ModpackSavePath, JsonConvert.SerializeObject(modpacks, Formatting.Indented));
            }
        }

        static public async Task RemoveModpack(MainWindow sender, string ModpackName)
        {
            Directory.Delete(Path.Combine(MinecraftPath.GetOSDefaultPath(), "Istances", ModpackName), true);

            if (File.Exists(ModpackSavePath))
            {
                Modpacks modpacks = JsonConvert.DeserializeObject<Modpacks>(await File.ReadAllTextAsync(ModpackSavePath));
                modpacks.modpack.Remove(ModpackName);

                File.Delete(ModpackSavePath);
                await File.WriteAllTextAsync(ModpackSavePath, JsonConvert.SerializeObject(modpacks, Formatting.Indented));

                List<string> items = new List<string>();
                foreach (KeyValuePair<string, Modpack> modpack in modpacks.modpack)
                {
                    items.Add(modpack.Value.Name);
                }
                sender.modpackCBox.Items = items;
            }
        }
    }
}