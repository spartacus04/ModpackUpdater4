using System.Collections.Generic;

namespace ModpackUpdater4
{
    public class LauncherProfile
    {
        public Dictionary<string, AuthenticationDatabase> authenticationDatabase { get; set; }
        public string clientToken { get; set; }
        public LauncherVersion launcherVersion { get; set; }
        public Dictionary<string, Profile> profiles { get; set; }
        public SelectedUser selectedUser { get; set; }
        public Settings settings { get; set; }
    }

    public class Profile
    {
        public string created { get; set; }
        public string gameDir { get; set; }
        public string icon { get; set; }
        public string javaArgs { get; set; }
        public string lastUsed { get; set; }
        public string lastVersionId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }

    public class LauncherVersion
    {
        public int format { get; set; }
        public string name { get; set; }
        public int profilesFormat { get; set; }
    }

    public class SelectedUser
    {
        public string account { get; set; }
        public string profile { get; set; }
    }

    public class Settings
    {
        public bool crashAssistance { get; set; }
        public bool enableAdvanced { get; set; }
        public bool enableAnalytics { get; set; }
        public bool enableHistorical { get; set; }
        public bool enableReleases { get; set; }
        public bool enableSnapshots { get; set; }
        public bool keepLauncherOpen { get; set; }
        public string profileSorting { get; set; }
        public bool showGameLog { get; set; }
        public bool showMenu { get; set; }
        public bool soundOn { get; set; }
    }

    public class AuthenticationDatabase
    {
        public string accessToken { get; set; }
        public Dictionary<string, UserProfiles> profiles { get; set; }
        public List<Proprieties> properties { get; set; }
        public string username { get; set; }
    }

    public class UserProfiles
    {
        public string displayName { get; set; }
    }

    public class Proprieties
    {
        public string name { get; set; }
        public string profileId { get; set; }
        public string userId { get; set; }
        public string value { get; set; }
    }
}