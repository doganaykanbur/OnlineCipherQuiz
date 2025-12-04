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
    }

    public class InMemoryRoomStore : IRoomStore
    {
        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly string _dataFolder;

        public InMemoryRoomStore()
        {
            _dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Rooms");
            Directory.CreateDirectory(_dataFolder);
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
    }
}
