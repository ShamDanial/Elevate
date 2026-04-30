using SQLite;

namespace Elevate.Data.Models
{
    [Table("AppAlerts")]
    public class AppAlert
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        [NotNull]
        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public string IconEmoji { get; set; } = "🔔";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }
}