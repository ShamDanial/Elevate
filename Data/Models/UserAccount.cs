using SQLite;

namespace Elevate.Data.Models
{
    /// <summary>
    /// Represents a registered user account.
    /// Maps to the "UserAccounts" table in the SQLite database.
    /// </summary>
    [Table("UserAccounts")]
    public class UserAccount
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [MaxLength(60), NotNull, Unique]
        public string Username { get; set; } = string.Empty;

        [NotNull]
        public string Email { get; set; } = string.Empty;

        /// <summary>BCrypt hash of the password.</summary>
        [NotNull]
        public string PasswordHash { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Simple settings flags stored as JSON string.</summary>
        public string SettingsJson { get; set; } = "{\"realTimeAlerts\":true,\"postReminders\":false,\"analyticsTracking\":true,\"autoBackup\":false,\"darkMode\":true}";
    }
}