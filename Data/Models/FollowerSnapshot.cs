using SQLite;

namespace Elevate.Data.Models
{
    [Table("FollowerSnapshots")]
    public class FollowerSnapshot
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;

        public double FollowerCount { get; set; }

        /// <summary>Net new followers gained on this day.</summary>
        public double DailyGain { get; set; }
    }
}