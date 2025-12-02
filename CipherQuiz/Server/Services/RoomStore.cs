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

        public Task<Room?> GetRoomAsync(string code)
        {
            _rooms.TryGetValue(code, out var room);
            return Task.FromResult(room);
        }

        public Task CreateRoomAsync(Room room)
        {
            _rooms.TryAdd(room.Code, room);
            return Task.CompletedTask;
        }

        public Task UpdateRoomAsync(Room room)
        {
            // In-memory reference type, so updates are often implicit if we modify the object directly.
            // But for a store interface, we might want to be explicit.
            // Since we are using a ConcurrentDictionary with reference types, getting the object and modifying it works.
            // This method is here for future DB implementations.
            return Task.CompletedTask;
        }

        public Task RemoveRoomAsync(string code)
        {
            _rooms.TryRemove(code, out _);
            return Task.CompletedTask;
        }
    }
}
