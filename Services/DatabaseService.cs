using SQLite;
using StudySync.Models;
using System.Collections.Generic;
using System.IO;
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
            _database.CreateTableAsync<Note>().Wait();
        }

        public Task<List<Note>> GetNotesAsync() =>
            _database.Table<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetPrivateNotesAsync() =>
            _database.Table<Note>().Where(n => !n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetSharedNotesAsync() =>
            _database.Table<Note>().Where(n => n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<int> SaveNoteAsync(Note note) =>
            note.Id == 0 ? _database.InsertAsync(note) : _database.UpdateAsync(note);

        public Task<int> DeleteNoteAsync(Note note) => _database.DeleteAsync(note);
        public Task<int> GetNotesCountAsync() => _database.Table<Note>().CountAsync();
    }
}