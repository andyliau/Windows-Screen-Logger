namespace WindowsScreenLogger
{
    /// <summary>
    /// Redacts window titles for processes that may display sensitive information
    /// (password managers, secure messaging apps, credential helpers).
    /// </summary>
    public class PrivacyFilter
    {
        private static readonly HashSet<string> BlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            // Password managers
            "KeePass", "KeePassXC", "1Password", "Bitwarden", "LastPass", "Dashlane", "NordPass",
            // Secure messaging
            "Signal", "Telegram", "WhatsApp",
            // SSH / remote access terminals
            "ssh", "putty", "kitty", "SecureCRT", "MobaXterm",
            // Windows credential UI
            "CredentialUIBroker", "consent", "lsass",
        };

        public bool IsBlocked(string processName)
            => BlockedProcesses.Contains(processName);

        public string FilterTitle(string processName, string title)
            => IsBlocked(processName) ? "[redacted]" : title;
    }
}
