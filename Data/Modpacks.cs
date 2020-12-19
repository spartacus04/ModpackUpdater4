using System.Collections.Generic;

namespace ModpackUpdater4
{
    internal class Modpacks
    {
        public Dictionary<string, Modpack> modpack { get; set; }
    }

    internal class Modpack
    {
        public string Name { get; set; }
        public string Token { get; set; }
    }
}