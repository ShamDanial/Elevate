using SQLite;
using Elevate.Data.Models;

namespace Elevate.Data
{
    /// <summary>
    /// Central SQLite database service for the Elevate app.
    /// Initialised once via MauiProgram and injected as a singleton.
    /// All data is user-scoped: every record stores the UserId of its owner.
    /// </summary>
    public class ElevateDatabase
    {
        private readonly SQLiteAsyncConnection _db;

        public ElevateDatabase(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        }

        /// <summary>Creates all tables. No seed data — users enter their own.</summary>
        public async Task InitAsync()
        {
            await _db.CreateTableAsync<UserAccount>();
            await _db.CreateTableAsync<Achievement>();
            await _db.CreateTableAsync<ContentItem>();
            await _db.CreateTableAsync<FollowerSnapshot>();
            await _db.CreateTableAsync<CreatorGoal>();
            await _db.CreateTableAsync<AppAlert>();
        }

        // ─── Session (in-memory, not persisted) ───────────────────────────────

        public int CurrentUserId { get; private set; } = 0;

        public void SetCurrentUser(int userId) => CurrentUserId = userId;

        // ─── Auth ──────────────────────────────────────────────────────────────

        public Task<UserAccount?> GetUserByUsernameAsync(string username) =>
            _db.Table<UserAccount>()
               .Where(u => u.Username == username)
               .FirstOrDefaultAsync();

        public Task<UserAccount?> GetUserByEmailAsync(string email) =>
            _db.Table<UserAccount>()
               .Where(u => u.Email == email)
               .FirstOrDefaultAsync();

        public Task<int> SaveUserAsync(UserAccount user) =>
            user.Id == 0 ? _db.InsertAsync(user) : _db.UpdateAsync(user);

        public async Task<UserAccount?> GetUserByIdAsync(int id)
        {
            var list = await _db.Table<UserAccount>().Where(u => u.Id == id).ToListAsync();
            return list.FirstOrDefault();
        }

        // ─── Achievements (user-scoped) ────────────────────────────────────────

        public Task<List<Achievement>> GetAchievementsAsync() =>
            _db.Table<Achievement>()
               .Where(a => a.UserId == CurrentUserId)
               .OrderByDescending(a => a.Date)
               .ToListAsync();

        public Task<List<Achievement>> GetRecentAchievementsAsync(int count = 3) =>
            _db.Table<Achievement>()
               .Where(a => a.UserId == CurrentUserId && !a.IsDraft)
               .OrderByDescending(a => a.Date)
               .Take(count)
               .ToListAsync();

        public Task<int> SaveAchievementAsync(Achievement achievement)
        {
            achievement.UserId = CurrentUserId;
            return achievement.Id == 0 ? _db.InsertAsync(achievement) : _db.UpdateAsync(achievement);
        }

        public Task<int> DeleteAchievementAsync(Achievement achievement) =>
            _db.DeleteAsync(achievement);

        // ─── Content Items (user-scoped) ───────────────────────────────────────

        public Task<List<ContentItem>> GetContentItemsAsync() =>
            _db.Table<ContentItem>()
               .Where(c => c.UserId == CurrentUserId)
               .OrderBy(c => c.ScheduledDate)
               .ThenBy(c => c.ScheduledTime)
               .ToListAsync();

        public Task<List<ContentItem>> GetUpcomingContentAsync(int count = 3)
        {
            var today = DateTime.Today;
            return _db.Table<ContentItem>()
                      .Where(c => c.UserId == CurrentUserId && !c.IsCompleted && c.ScheduledDate >= today)
                      .OrderBy(c => c.ScheduledDate)
                      .Take(count)
                      .ToListAsync();
        }

        public Task<int> GetCompletedCountAsync() =>
            _db.Table<ContentItem>()
               .Where(c => c.UserId == CurrentUserId && c.IsCompleted)
               .CountAsync();

        public Task<int> SaveContentItemAsync(ContentItem item)
        {
            item.UserId = CurrentUserId;
            return item.Id == 0 ? _db.InsertAsync(item) : _db.UpdateAsync(item);
        }

        public Task<int> DeleteContentItemAsync(ContentItem item) =>
            _db.DeleteAsync(item);

        // ─── Follower Snapshots (user-scoped) ──────────────────────────────────

        public async Task<List<FollowerSnapshot>> GetSnapshotsAsync(int days = 30)
        {
            // Order by Id descending so multiple same-day entries always show
            // the most recently inserted one first.
            var all = await _db.Table<FollowerSnapshot>()
                                .Where(s => s.UserId == CurrentUserId)
                                .OrderByDescending(s => s.Id)
                                .ToListAsync();
            // Deduplicate: keep only the latest entry per calendar day
            return all
                .GroupBy(s => s.Date.Date)
                .Select(g => g.First())   // First() = highest Id = most recent insert
                .OrderByDescending(g => g.Date)
                .Take(days)
                .ToList();
        }

        public async Task<FollowerSnapshot?> GetLatestSnapshotAsync() =>
            (await _db.Table<FollowerSnapshot>()
                      .Where(s => s.UserId == CurrentUserId)
                      .OrderByDescending(s => s.Id)
                      .Take(1)
                      .ToListAsync())
            .FirstOrDefault();

        public Task<int> SaveSnapshotAsync(FollowerSnapshot snapshot)
        {
            snapshot.UserId = CurrentUserId;
            return snapshot.Id == 0 ? _db.InsertAsync(snapshot) : _db.UpdateAsync(snapshot);
        }

        // ─── Likes helpers (read from Achievement table) ───────────────────────

        /// <summary>Returns the most recently logged Likes achievement for the current user.</summary>
        public async Task<Achievement?> GetLatestLikesAsync() =>
            (await _db.Table<Achievement>()
                      .Where(a => a.UserId == CurrentUserId && a.IconEmoji == "❤️")
                      .OrderByDescending(a => a.Date)
                      .Take(1)
                      .ToListAsync())
            .FirstOrDefault();

        /// <summary>Returns the second-most-recently logged Likes achievement (used for delta calculation).</summary>
        public async Task<Achievement?> GetPreviousLikesAsync() =>
            (await _db.Table<Achievement>()
                      .Where(a => a.UserId == CurrentUserId && a.IconEmoji == "❤️")
                      .OrderByDescending(a => a.Date)
                      .Skip(1)
                      .Take(1)
                      .ToListAsync())
            .FirstOrDefault();

        // ─── Creator Goals (user-scoped) ───────────────────────────────────────

        /// <summary>Returns only user-created (non-milestone) goals.</summary>
        public Task<List<CreatorGoal>> GetGoalsAsync() =>
            _db.Table<CreatorGoal>()
               .Where(g => g.UserId == CurrentUserId && !g.IsMilestone)
               .ToListAsync();

        /// <summary>Returns only preset milestone records for the current user.</summary>
        public Task<List<CreatorGoal>> GetMilestonesAsync() =>
            _db.Table<CreatorGoal>()
               .Where(g => g.UserId == CurrentUserId && g.IsMilestone)
               .OrderBy(g => g.TargetValue)
               .ToListAsync();

        public async Task<CreatorGoal?> GetPrimaryGoalAsync() =>
            (await _db.Table<CreatorGoal>()
                      .Where(g => g.UserId == CurrentUserId && !g.IsMilestone)
                      .Take(1)
                      .ToListAsync())
            .FirstOrDefault();

        public Task<int> SaveGoalAsync(CreatorGoal goal)
        {
            goal.UserId = CurrentUserId;
            return goal.Id == 0 ? _db.InsertAsync(goal) : _db.UpdateAsync(goal);
        }

        public Task<int> DeleteGoalAsync(CreatorGoal goal) =>
            _db.DeleteAsync(goal);

        // ─── Milestone seeding ─────────────────────────────────────────────────

        /// <summary>
        /// Seeds the standard follower and likes milestones for a user the first
        /// time they log in. Safe to call multiple times — skips if already seeded.
        /// </summary>
        public async Task SeedMilestonesAsync(int userId)
        {
            // Check if already seeded
            int existing = await _db.Table<CreatorGoal>()
                                    .Where(g => g.UserId == userId && g.IsMilestone)
                                    .CountAsync();
            if (existing > 0) return;

            var followerMilestones = new[]
            {
                (100,      "First 100",        "Your first 100 followers 🌱",          "🌱", "#4CAF50"),
                (500,      "500 Club",         "Half way to 1K!",                      "⭐", "#8BC34A"),
                (1_000,    "1K Milestone",     "You hit 1,000 followers!",             "🎉", "#6C63FF"),
                (5_000,    "5K Strong",        "5,000 people follow your journey",     "🔥", "#FF7043"),
                (10_000,   "10K Creator",      "You're officially a 10K creator",      "💎", "#00BCD4"),
                (50_000,   "50K Influencer",   "50K — brands are watching",            "🚀", "#9C27B0"),
                (100_000,  "100K Legend",      "Six-figure following achieved",        "👑", "#FFD700"),
            };

            var likesMilestones = new[]
            {
                (1_000,    "1K Likes",         "First 1,000 likes earned",             "❤️",  "#E53935"),
                (10_000,   "10K Likes",        "10,000 likes across your content",     "💖",  "#E91E63"),
                (100_000,  "100K Likes",       "100K hearts from your audience",       "💯",  "#FF4081"),
                (1_000_000,"1M Likes",         "One million likes — iconic",           "🌟",  "#FF6D00"),
            };

            var deadline = DateTime.Today.AddYears(2); // milestones have no hard deadline

            foreach (var (target, title, desc, badge, color) in followerMilestones)
            {
                await _db.InsertAsync(new CreatorGoal
                {
                    UserId = userId,
                    Title = title,
                    Description = desc,
                    Unit = "followers",
                    TargetValue = target,
                    CurrentValue = 0,
                    Deadline = deadline,
                    AccentColor = color,
                    IsMilestone = true,
                    IsUnlocked = false,
                    MilestoneBadge = badge
                });
            }

            foreach (var (target, title, desc, badge, color) in likesMilestones)
            {
                await _db.InsertAsync(new CreatorGoal
                {
                    UserId = userId,
                    Title = title,
                    Description = desc,
                    Unit = "likes",
                    TargetValue = target,
                    CurrentValue = 0,
                    Deadline = deadline,
                    AccentColor = color,
                    IsMilestone = true,
                    IsUnlocked = false,
                    MilestoneBadge = badge
                });
            }
        }

        /// <summary>
        /// Called every time a follower or likes value is logged.
        /// Updates CurrentValue on all matching milestones, unlocks any that
        /// have crossed their threshold for the first time, and fires an alert.
        /// Returns the list of newly unlocked milestones.
        /// </summary>
        public async Task<List<CreatorGoal>> CheckAndUnlockMilestonesAsync(string unit, double newValue)
        {
            var milestones = await _db.Table<CreatorGoal>()
                                      .Where(g => g.UserId == CurrentUserId
                                               && g.IsMilestone
                                               && g.Unit == unit)
                                      .ToListAsync();

            var newlyUnlocked = new List<CreatorGoal>();

            foreach (var m in milestones)
            {
                m.CurrentValue = newValue;

                bool justUnlocked = !m.IsUnlocked && newValue >= m.TargetValue;
                if (justUnlocked)
                {
                    m.IsUnlocked = true;
                    newlyUnlocked.Add(m);

                    await SaveAlertAsync(new AppAlert
                    {
                        Title = $"{m.MilestoneBadge} Milestone Unlocked: {m.Title}",
                        Body = m.Description,
                        IconEmoji = m.MilestoneBadge,
                        UserId = CurrentUserId
                    });
                }

                await _db.UpdateAsync(m);
            }

            return newlyUnlocked;
        }

        // ─── App Alerts (user-scoped) ──────────────────────────────────────────

        public Task<List<AppAlert>> GetAlertsAsync() =>
            _db.Table<AppAlert>()
               .Where(a => a.UserId == CurrentUserId)
               .OrderByDescending(a => a.CreatedAt)
               .ToListAsync();

        public Task<int> GetUnreadCountAsync() =>
            _db.Table<AppAlert>()
               .Where(a => a.UserId == CurrentUserId && !a.IsRead)
               .CountAsync();

        public async Task MarkAllAlertsReadAsync()
        {
            var alerts = await _db.Table<AppAlert>()
                                  .Where(a => a.UserId == CurrentUserId && !a.IsRead)
                                  .ToListAsync();
            foreach (var alert in alerts)
            {
                alert.IsRead = true;
                await _db.UpdateAsync(alert);
            }
        }

        public Task<int> SaveAlertAsync(AppAlert alert)
        {
            alert.UserId = CurrentUserId;
            return alert.Id == 0 ? _db.InsertAsync(alert) : _db.UpdateAsync(alert);
        }

        // ─── Settings ──────────────────────────────────────────────────────────

        public async Task SaveSettingsAsync(string settingsJson)
        {
            var user = await GetUserByIdAsync(CurrentUserId);
            if (user != null)
            {
                user.SettingsJson = settingsJson;
                await SaveUserAsync(user);
            }
        }

        public async Task<string> GetSettingsAsync()
        {
            var user = await GetUserByIdAsync(CurrentUserId);
            return user?.SettingsJson ?? "{\"realTimeAlerts\":true,\"postReminders\":false,\"analyticsTracking\":true,\"autoBackup\":false,\"darkMode\":true}";
        }

        // ─── Clear / Reset ─────────────────────────────────────────────────────

        public async Task ClearAllDataAsync()
        {
            await _db.ExecuteAsync($"DELETE FROM Achievements WHERE UserId = ?", CurrentUserId);
            await _db.ExecuteAsync($"DELETE FROM ContentItems WHERE UserId = ?", CurrentUserId);
            await _db.ExecuteAsync($"DELETE FROM FollowerSnapshots WHERE UserId = ?", CurrentUserId);
            await _db.ExecuteAsync($"DELETE FROM CreatorGoals WHERE UserId = ?", CurrentUserId);
            await _db.ExecuteAsync($"DELETE FROM AppAlerts WHERE UserId = ?", CurrentUserId);
        }
    }
}