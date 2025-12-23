using CipherQuiz.Shared;
using System.Collections.Concurrent;

namespace CipherQuiz.Server.Services
{
    public interface IRoomStore
    {
        Task<Room?> GetRoomAsync(string code);
        Task CreateRoomAsync(Room room);
        Task UpdateRoomAsync(Room room);
        Task RemoveRoomAsync(string code);
        
        // Archiving
        Task ArchiveRoomAsync(Room room);
        Task<List<Room>> GetArchivedRoomsAsync();
        Task<Room?> GetArchivedRoomAsync(string code);
    }

    public class InMemoryRoomStore : IRoomStore
    {
        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly string _dataFolder;
        private readonly string _archiveFolder;

        public InMemoryRoomStore()
        {
            _dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Rooms");
            _archiveFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "RoomArchives");
            Directory.CreateDirectory(_dataFolder);
            Directory.CreateDirectory(_archiveFolder);
            LoadRooms();
        }

        private void LoadRooms()
        {
            try
            {
                var files = Directory.GetFiles(_dataFolder, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var room = System.Text.Json.JsonSerializer.Deserialize<Room>(json);
                        if (room != null)
                        {
                            _rooms.TryAdd(room.Code, room);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading room file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing data directory: {ex.Message}");
            }
        }

        private void SaveRoom(Room room)
        {
            try
            {
                var filePath = Path.Combine(_dataFolder, $"{room.Code}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(room, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving room {room.Code}: {ex.Message}");
            }
        }

        public Task<Room?> GetRoomAsync(string code)
        {
            _rooms.TryGetValue(code, out var room);
            return Task.FromResult(room);
        }

        public Task CreateRoomAsync(Room room)
        {
            _rooms.TryAdd(room.Code, room);
            SaveRoom(room);
            return Task.CompletedTask;
        }

        public Task UpdateRoomAsync(Room room)
        {
            // In-memory update is implicit since it's a reference type,
            // but we MUST save to file to persist changes.
            if (_rooms.ContainsKey(room.Code))
            {
                SaveRoom(room);
            }
            return Task.CompletedTask;
        }

        public Task RemoveRoomAsync(string code)
        {
            if (_rooms.TryRemove(code, out _))
            {
                var filePath = Path.Combine(_dataFolder, $"{code}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            return Task.CompletedTask;
        }

        public Task ArchiveRoomAsync(Room room)
        {
            try
            {
                var filePath = Path.Combine(_archiveFolder, $"{room.Code}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(room, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error archiving room {room.Code}: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task<List<Room>> GetArchivedRoomsAsync()
        {
            var list = new List<Room>();
            try
            {
                var files = Directory.GetFiles(_archiveFolder, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var room = System.Text.Json.JsonSerializer.Deserialize<Room>(json);
                        if (room != null) list.Add(room);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing archives: {ex.Message}");
            }
            return Task.FromResult(list.OrderByDescending(r => r.StartUtc).ToList());
        }

        public Task<Room?> GetArchivedRoomAsync(string code)
        {
             // This is a bit tricky since filename has timestamp. 
             // We'll iterate and find by code property inside json or filename starts with code.
             // Simpler: let's rely on finding by content or filename pattern.
             // Helper logic:
             try 
             {
                 var files = Directory.GetFiles(_archiveFolder, $"{code}*.json"); // Assuming code is unique prefix
                 foreach(var f in files)
                 {
                     var json = File.ReadAllText(f);
                     var room = System.Text.Json.JsonSerializer.Deserialize<Room>(json);
                     if (room != null && room.Code == code) return Task.FromResult<Room?>(room);
                 }
             }
             catch { }
             return Task.FromResult<Room?>(null);
        }
    }
}
