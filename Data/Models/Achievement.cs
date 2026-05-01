using SQLite;

namespace Elevate.Data.Models
{
    [Table("Achievements")]
    public class Achievement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Owner of this record.</summary>
        [Indexed]
        public int UserId { get; set; }

        [MaxLength(80), NotNull]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The numeric value the creator wants to log alongside this achievement.
        /// Examples: follower count reached, number of views, revenue amount, etc.
        /// It's whatever number best represents "how big" this win is.
        /// </summary>
        public double MetricValue { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>Category icon emoji (e.g. "👥", "📹", "📺").</summary>
        public string IconEmoji { get; set; } = "🏆";

        /// <summary>Whether this achievement is a draft or fully saved.</summary>
        public bool IsDraft { get; set; } = false;
    }
}