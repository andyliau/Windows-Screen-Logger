namespace WindowsActivityLogger
{
    /// <summary>
    /// Maps process names to coarse activity categories that help an AI
    /// distinguish productive work from entertainment/idle time.
    /// </summary>
    public static class CategoryHints
    {
        private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
        {
            // IDEs / editors
            { "devenv",         "ide" },
            { "code",           "ide" },
            { "rider",          "ide" },
            { "idea64",         "ide" },
            { "pycharm64",      "ide" },
            { "webstorm64",     "ide" },
            { "notepad++",      "editor" },
            { "notepad",        "editor" },
            { "sublime_text",   "editor" },

            // Browsers
            { "chrome",         "browser" },
            { "msedge",         "browser" },
            { "firefox",        "browser" },
            { "brave",          "browser" },
            { "opera",          "browser" },
            { "vivaldi",        "browser" },

            // Communication
            { "teams",          "comms" },
            { "slack",          "comms" },
            { "outlook",        "comms" },
            { "thunderbird",    "comms" },
            { "zoom",           "comms" },
            { "discord",        "comms" },
            { "signal",         "comms" },
            { "telegram",       "comms" },

            // Terminals / shells
            { "WindowsTerminal","terminal" },
            { "powershell",     "terminal" },
            { "pwsh",           "terminal" },
            { "cmd",            "terminal" },
            { "wsl",            "terminal" },
            { "putty",          "terminal" },

            // Productivity / documents
            { "winword",        "docs" },
            { "excel",          "docs" },
            { "powerpnt",       "docs" },
            { "onenote",        "docs" },
            { "acrobat",        "docs" },

            // Design / media creation
            { "figma",          "design" },
            { "photoshop",      "design" },
            { "illustrator",    "design" },
            { "gimp",           "design" },
            { "blender",        "design" },

            // Gaming
            { "steam",          "gaming" },
            { "EpicGamesLauncher", "gaming" },
            { "Battle.net",     "gaming" },
            { "LeagueClient",   "gaming" },
            { "javaw",          "gaming" },  // Minecraft
            { "RobloxPlayerBeta","gaming" },
            { "Cyberpunk2077",  "gaming" },

            // Entertainment / media consumption
            { "spotify",        "entertainment" },
            { "vlc",            "entertainment" },
            { "wmplayer",       "entertainment" },
            { "Netflix",        "entertainment" },
            { "DisneyPlus",     "entertainment" },
            { "YouTube",        "entertainment" },
            { "Prime Video",    "entertainment" },
        };

        /// <summary>Returns a category string, or "other" if the process is not in the hint table.</summary>
        public static string Categorize(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return "other";
            return Hints.TryGetValue(processName, out var cat) ? cat : "other";
        }
    }
}
