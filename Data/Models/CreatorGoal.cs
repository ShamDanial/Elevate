using SQLite;

namespace Elevate.Data.Models
{
    [Table("CreatorGoals")]
    public class CreatorGoal
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        [NotNull]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>e.g. "followers", "likes"</summary>
        public string Unit { get; set; } = string.Empty;

        public double CurrentValue { get; set; }

        public double TargetValue { get; set; }

        public DateTime Deadline { get; set; }

        /// <summary>Accent colour hex shown on the gauge.</summary>
        public string AccentColor { get; set; } = "#6C63FF";

        // ── Milestone fields ──────────────────────────────────────────────

        /// <summary>
        /// True = this is a preset system milestone (seeded on first login).
        /// False = user-created custom goal.
        /// </summary>
        public bool IsMilestone { get; set; } = false;

        /// <summary>
        /// For milestones: has the user's logged value crossed TargetValue yet?
        /// Always false for custom goals (they use Progress instead).
        /// </summary>
        public bool IsUnlocked { get; set; } = false;

        /// <summary>Emoji badge shown on the milestone card.</summary>
        public string MilestoneBadge { get; set; } = "🏆";

        // ── Computed (not stored) ─────────────────────────────────────────

        [Ignore]
        public double Progress => TargetValue > 0
            ? Math.Min(CurrentValue / TargetValue, 1.0)
            : 0;

        [Ignore]
        public string ProgressLabel => $"{(int)(Progress * 100)}%";
    }
}