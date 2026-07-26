namespace FluentLauncher.Models
{
    public enum AccountType { Microsoft, Offline }
    public class Account
    {
        public string Username { get; set; } = "";
        public string UUID { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public AccountType Type { get; set; }
        public string AvatarUrl => $"https://minotar.net/helm/{Username}/64.png";
    }
}
