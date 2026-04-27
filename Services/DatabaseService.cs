using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmsGatewayApp.Models;

namespace SmsGatewayApp.Services
{
    public class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
        public static DatabaseService Instance => _instance.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;
        public string DbPath => _dbPath;

        private DatabaseService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmsGatewayApp");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _dbPath = Path.Combine(folder, "sms_gateway.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var commands = new[]
            {
                @"CREATE TABLE IF NOT EXISTS ExcelGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    CreatedAt DATETIME NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS SmsTemplates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    MessageBody TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS SmsContacts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    Phone TEXT NOT NULL,
                    Name TEXT,
                    FOREIGN KEY(GroupId) REFERENCES ExcelGroups(Id) ON DELETE CASCADE,
                    UNIQUE(GroupId, Phone)
                );",
                @"CREATE TABLE IF NOT EXISTS SmsHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContactId INTEGER NOT NULL,
                    MessageBody TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    SentAt DATETIME NOT NULL,
                    FOREIGN KEY(ContactId) REFERENCES SmsContacts(Id) ON DELETE CASCADE
                );",
                @"CREATE TABLE IF NOT EXISTS Blacklist (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Phone TEXT NOT NULL UNIQUE,
                    Reason TEXT,
                    AddedAt DATETIME NOT NULL
                );"
            };

            foreach (var cmdText in commands)
            {
                using var command = new SqliteCommand(cmdText, connection);
                await command.ExecuteNonQueryAsync();
            }

        }

        // ── Groups ──────────────────────────────────────────────────────────────

        public async Task<int> InsertExcelGroupAsync(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmdText = "INSERT INTO ExcelGroups (Name, CreatedAt) VALUES (@name, @date); SELECT last_insert_rowid();";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@date", DateTime.Now);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateGroupAsync(int id, string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("UPDATE ExcelGroups SET Name = @name WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteGroupAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();
            try
            {
                using var cmd1 = new SqliteCommand("DELETE FROM SmsContacts WHERE GroupId = @id", connection, tx);
                cmd1.Parameters.AddWithValue("@id", id);
                await cmd1.ExecuteNonQueryAsync();

                using var cmd2 = new SqliteCommand("DELETE FROM ExcelGroups WHERE Id = @id", connection, tx);
                cmd2.Parameters.AddWithValue("@id", id);
                await cmd2.ExecuteNonQueryAsync();

                await tx.CommitAsync();
            }
            catch { await tx.RollbackAsync(); throw; }
        }

        public async Task<List<ExcelGroup>> GetGroupsAsync()
        {
            var groups = new List<ExcelGroup>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT * FROM ExcelGroups ORDER BY CreatedAt DESC", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                groups.Add(new ExcelGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    CreatedAt = reader.GetDateTime(2)
                });
            return groups;
        }

        // ── Contacts ────────────────────────────────────────────────────────────

        public async Task BulkInsertContactsAsync(int groupId, List<(string Phone, string? Name)> contacts)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();
            try
            {
                const string cmdText = "INSERT OR IGNORE INTO SmsContacts (GroupId, Phone, Name) VALUES (@groupId, @phone, @name)";
                using var command = new SqliteCommand(cmdText, connection, tx);
                command.Parameters.Add("@groupId", SqliteType.Integer);
                command.Parameters.Add("@phone", SqliteType.Text);
                command.Parameters.Add("@name", SqliteType.Text);

                foreach (var contact in contacts)
                {
                    command.Parameters["@groupId"].Value = groupId;
                    command.Parameters["@phone"].Value = contact.Phone;
                    command.Parameters["@name"].Value = (object?)contact.Name ?? DBNull.Value;
                    await command.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
            catch { await tx.RollbackAsync(); throw; }
        }

        public async Task<List<SmsContact>> GetContactsByGroupAsync(int groupId)
        {
            var contacts = new List<SmsContact>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT * FROM SmsContacts WHERE GroupId = @groupId", connection);
            command.Parameters.AddWithValue("@groupId", groupId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                contacts.Add(new SmsContact
                {
                    Id = reader.GetInt32(0),
                    GroupId = reader.GetInt32(1),
                    Phone = reader.GetString(2),
                    Name = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            return contacts;
        }

        public async Task InsertContactAsync(int groupId, string phone, string? name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "INSERT INTO SmsContacts (GroupId, Phone, Name) VALUES (@groupId, @phone, @name)", connection);
            command.Parameters.AddWithValue("@groupId", groupId);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateContactAsync(int id, string phone, string? name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "UPDATE SmsContacts SET Phone = @phone, Name = @name WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteContactAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("DELETE FROM SmsContacts WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        // ── Templates ───────────────────────────────────────────────────────────

        public async Task<List<SmsTemplate>> GetTemplatesAsync()
        {
            var templates = new List<SmsTemplate>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT * FROM SmsTemplates", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                templates.Add(new SmsTemplate
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    MessageBody = reader.GetString(2)
                });
            return templates;
        }

        public async Task SaveTemplateAsync(string title, string body)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "INSERT INTO SmsTemplates (Title, MessageBody) VALUES (@title, @body)", connection);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@body", body);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateTemplateAsync(int id, string title, string body)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "UPDATE SmsTemplates SET Title = @title, MessageBody = @body WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@body", body);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteTemplateAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("DELETE FROM SmsTemplates WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        // ── History ─────────────────────────────────────────────────────────────

        public async Task AddHistoryAsync(int contactId, string message, string status)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "INSERT INTO SmsHistory (ContactId, MessageBody, Status, SentAt) VALUES (@contactId, @message, @status, @sentAt)",
                connection);
            command.Parameters.AddWithValue("@contactId", contactId);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@sentAt", DateTime.Now);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<SmsHistoryEntry>> GetHistoryByContactAsync(int contactId)
        {
            var history = new List<SmsHistoryEntry>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "SELECT * FROM SmsHistory WHERE ContactId = @contactId ORDER BY SentAt DESC", connection);
            command.Parameters.AddWithValue("@contactId", contactId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                history.Add(new SmsHistoryEntry
                {
                    Id = reader.GetInt32(0),
                    ContactId = reader.GetInt32(1),
                    MessageBody = reader.GetString(2),
                    Status = reader.GetString(3),
                    SentAt = reader.GetDateTime(4)
                });
            return history;
        }

        public async Task<List<SmsHistoryEntry>> GetAllHistoryAsync(int limit = 500)
        {
            var history = new List<SmsHistoryEntry>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                @"SELECT h.Id, h.ContactId, c.Name, c.Phone, h.MessageBody, h.Status, h.SentAt 
                  FROM SmsHistory h
                  LEFT JOIN SmsContacts c ON h.ContactId = c.Id
                  ORDER BY h.SentAt DESC LIMIT @limit", connection);
            command.Parameters.AddWithValue("@limit", limit);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                history.Add(new SmsHistoryEntry
                {
                    Id = reader.GetInt32(0),
                    ContactId = reader.GetInt32(1),
                    ContactName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ContactPhone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MessageBody = reader.GetString(4),
                    Status = reader.GetString(5),
                    SentAt = reader.GetDateTime(6)
                });
            return history;
        }

        public async Task DeleteHistoryItemAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SmsHistory WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }

        // ── Blacklist ───────────────────────────────────────────────────────────

        public async Task<List<BlacklistEntry>> GetBlacklistAsync()
        {
            var list = new List<BlacklistEntry>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT * FROM Blacklist ORDER BY AddedAt DESC", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new BlacklistEntry
                {
                    Id = reader.GetInt32(0),
                    Phone = reader.GetString(1),
                    Reason = reader.IsDBNull(2) ? null : reader.GetString(2),
                    AddedAt = reader.GetDateTime(3)
                });
            return list;
        }

        public async Task AddToBlacklistAsync(string phone, string? reason = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(
                "INSERT OR IGNORE INTO Blacklist (Phone, Reason, AddedAt) VALUES (@phone, @reason, @addedAt)", connection);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
            command.Parameters.AddWithValue("@addedAt", DateTime.Now);
            await command.ExecuteNonQueryAsync();
        }

        public async Task RemoveFromBlacklistAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("DELETE FROM Blacklist WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> IsBlacklistedAsync(string phone)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT COUNT(*) FROM Blacklist WHERE Phone = @phone", connection);
            command.Parameters.AddWithValue("@phone", phone);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        // ── Backup / Restore ────────────────────────────────────────────────────

        public async Task<string> BackupDatabaseAsync(string targetZipPath)
        {
            string tempDbPath = Path.Combine(Path.GetTempPath(), $"sms_backup_{Guid.NewGuid()}.db");
            
            try
            {
                if (File.Exists(tempDbPath)) File.Delete(tempDbPath);

                // Use official SQLite Backup API to create a consistent copy of the DB
                using (var connection = new SqliteConnection(_connectionString))
                using (var destination = new SqliteConnection($"Data Source={tempDbPath}"))
                {
                    await connection.OpenAsync();
                    await destination.OpenAsync();
                    connection.BackupDatabase(destination);
                    
                    // Clear pool for destination to release the file lock immediately
                    SqliteConnection.ClearPool(destination);
                }

                // Create ZIP and add the temp DB
                if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
                using (var zip = ZipFile.Open(targetZipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tempDbPath, "sms_gateway.db");
                }

                return targetZipPath;
            }
            finally
            {
                if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
            }
        }

        public async Task RestoreDatabaseAsync(string zipPath)
        {
            // Checkpoint WAL before overwrite
            using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = new SqliteCommand("PRAGMA wal_checkpoint(TRUNCATE);", conn);
                await cmd.ExecuteNonQueryAsync();
            }

            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                if (entry.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(_dbPath, overwrite: true);
                    break;
                }
            }
        }
        public async Task<Dictionary<string, int>> GetStatsAsync()
        {
            var stats = new Dictionary<string, int>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Contacts count
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM SmsContacts", connection))
                stats["Contacts"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Groups count
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM ExcelGroups", connection))
                stats["Groups"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Sent count
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM SmsHistory WHERE Status = 'Sent'", connection))
                stats["Sent"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Failed count
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM SmsHistory WHERE Status != 'Sent'", connection))
                stats["Failed"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return stats;
        }
    }
}
