using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmsGatewayApp.Models;

namespace SmsGatewayApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath = "sms_gateway.db";
        private readonly string _connectionString;

        public DatabaseService()
        {
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
                @"CREATE TABLE IF NOT EXISTS SmsContacts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    Phone TEXT NOT NULL UNIQUE,
                    Name TEXT,
                    FOREIGN KEY(GroupId) REFERENCES ExcelGroups(Id)
                );",
                @"CREATE TABLE IF NOT EXISTS SmsTemplates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    MessageBody TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS SmsHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContactId INTEGER NOT NULL,
                    MessageBody TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    SentAt DATETIME NOT NULL,
                    FOREIGN KEY(ContactId) REFERENCES SmsContacts(Id) ON DELETE CASCADE
                );"
            };

            foreach (var cmdText in commands)
            {
                using var command = new SqliteCommand(cmdText, connection);
                await command.ExecuteNonQueryAsync();
            }
        }

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
            var cmdText = "UPDATE ExcelGroups SET Name = @name WHERE Id = @id";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteGroupAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete contacts first
                var delContacts = "DELETE FROM SmsContacts WHERE GroupId = @id";
                using var cmd1 = new SqliteCommand(delContacts, connection, transaction);
                cmd1.Parameters.AddWithValue("@id", id);
                await cmd1.ExecuteNonQueryAsync();

                // Delete group
                var delGroup = "DELETE FROM ExcelGroups WHERE Id = @id";
                using var cmd2 = new SqliteCommand(delGroup, connection, transaction);
                cmd2.Parameters.AddWithValue("@id", id);
                await cmd2.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        public async Task BulkInsertContactsAsync(int groupId, List<string> phones)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var cmdText = "INSERT INTO SmsContacts (GroupId, Phone, Name) VALUES (@groupId, @phone, NULL)";
                using var command = new SqliteCommand(cmdText, connection, transaction);
                command.Parameters.Add("@groupId", SqliteType.Integer);
                command.Parameters.Add("@phone", SqliteType.Text);

                foreach (var phone in phones)
                {
                    command.Parameters["@groupId"].Value = groupId;
                    command.Parameters["@phone"].Value = phone;
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ExcelGroup>> GetGroupsAsync()
        {
            var groups = new List<ExcelGroup>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "SELECT * FROM ExcelGroups ORDER BY CreatedAt DESC";
            using var command = new SqliteCommand(cmdText, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                groups.Add(new ExcelGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    CreatedAt = reader.GetDateTime(2)
                });
            }
            return groups;
        }

        public async Task<List<SmsContact>> GetContactsByGroupAsync(int groupId)
        {
            var contacts = new List<SmsContact>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "SELECT * FROM SmsContacts WHERE GroupId = @groupId";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@groupId", groupId);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                contacts.Add(new SmsContact
                {
                    Id = reader.GetInt32(0),
                    GroupId = reader.GetInt32(1),
                    Phone = reader.GetString(2),
                    Name = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }
            return contacts;
        }

        public async Task SaveTemplateAsync(string title, string body)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "INSERT INTO SmsTemplates (Title, MessageBody) VALUES (@title, @body)";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@body", body);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateTemplateAsync(int id, string title, string body)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmdText = "UPDATE SmsTemplates SET Title = @title, MessageBody = @body WHERE Id = @id";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@body", body);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteTemplateAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmdText = "DELETE FROM SmsTemplates WHERE Id = @id";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task InsertContactAsync(int groupId, string phone, string? name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "INSERT INTO SmsContacts (GroupId, Phone, Name) VALUES (@groupId, @phone, @name)";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@groupId", groupId);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateContactAsync(int id, string phone, string? name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmdText = "UPDATE SmsContacts SET Phone = @phone, Name = @name WHERE Id = @id";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteContactAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmdText = "DELETE FROM SmsContacts WHERE Id = @id";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddHistoryAsync(int contactId, string message, string status)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "INSERT INTO SmsHistory (ContactId, MessageBody, Status, SentAt) VALUES (@contactId, @message, @status, @sentAt)";
            using var command = new SqliteCommand(cmdText, connection);
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

            var cmdText = "SELECT * FROM SmsHistory WHERE ContactId = @contactId ORDER BY SentAt DESC";
            using var command = new SqliteCommand(cmdText, connection);
            command.Parameters.AddWithValue("@contactId", contactId);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                history.Add(new SmsHistoryEntry
                {
                    Id = reader.GetInt32(0),
                    ContactId = reader.GetInt32(1),
                    MessageBody = reader.GetString(2),
                    Status = reader.GetString(3),
                    SentAt = reader.GetDateTime(4)
                });
            }
            return history;
        }

        public async Task<List<SmsTemplate>> GetTemplatesAsync()
        {
            var templates = new List<SmsTemplate>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmdText = "SELECT * FROM SmsTemplates";
            using var command = new SqliteCommand(cmdText, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                templates.Add(new SmsTemplate
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    MessageBody = reader.GetString(2)
                });
            }
            return templates;
        }
        public async Task DeleteHistoryItemAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SmsHistory WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }
    }
}
