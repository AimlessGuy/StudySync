using SQLite;
using StudySync.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StudySync.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "studysync.db3");

        public DatabaseService()
        {
            _database = new SQLiteAsyncConnection(DbPath);
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeDatabaseAsync()
        {
            await _database.CreateTableAsync<Note>().ConfigureAwait(false);
            await _database.CreateTableAsync<Section>().ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "PrimarySubjectTag", "TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "SecondaryTags", "TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
        }

        private async Task EnsureColumnExistsAsync(string tableName, string columnName, string definition)
        {
            var columns = await _database.QueryAsync<TableColumnInfo>($"PRAGMA table_info({tableName})").ConfigureAwait(false);
            if (columns.Any(column => column.name == columnName))
                return;

            await _database.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}").ConfigureAwait(false);
        }

        private sealed class TableColumnInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
        }

        public Task<List<Note>> GetNotesAsync() =>
            _database.Table<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetPrivateNotesAsync() =>
            _database.Table<Note>().Where(n => !n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetSharedNotesAsync() =>
            _database.Table<Note>().Where(n => n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<int> SaveNoteAsync(Note note) =>
            note.Id == 0 ? _database.InsertAsync(note) : _database.UpdateAsync(note);

        public Task<List<Section>> GetSectionsAsync() =>
            _database.Table<Section>().OrderBy(s => s.Name).ToListAsync();

        public async Task<Section?> GetSectionByInviteCodeAsync(string inviteCode)
        {
            var matches = await _database.Table<Section>()
                .Where(section => section.InviteCode == inviteCode)
                .ToListAsync();

            return matches.FirstOrDefault();
        }

        public async Task<int> SaveSectionAsync(Section section)
        {
            var existing = await GetSectionByInviteCodeAsync(section.InviteCode);
            if (existing != null)
            {
                section.Id = existing.Id;
                return await _database.UpdateAsync(section);
            }

            return await _database.InsertAsync(section);
        }

        public Task<int> UpvoteNoteAsync(Note note)
        {
            note.Upvotes++;
            return _database.UpdateAsync(note);
        }

        public Task<int> DeleteNoteAsync(Note note) => _database.DeleteAsync(note);
        public Task<int> GetNotesCountAsync() => _database.Table<Note>().CountAsync();
    }
}
