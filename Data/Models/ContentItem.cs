using SQLite;

namespace Elevate.Data.Models
{
    [Table("ContentItems")]
    public class ContentItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        [NotNull]
        public string Title { get; set; } = string.Empty;

        /// <summary>"Video" or "Livestream"</summary>
        public string ContentType { get; set; } = "Video";

        public DateTime ScheduledDate { get; set; } = DateTime.Now;

        public TimeSpan ScheduledTime { get; set; } = TimeSpan.Zero;

        public string Description { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
    }
}