using CipherQuiz.Server.Services;
using CipherQuiz.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CipherQuiz.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArchiveController : ControllerBase
    {
        private readonly IRoomStore _roomStore;

        public ArchiveController(IRoomStore roomStore)
        {
            _roomStore = roomStore;
        }

        [HttpGet]
        public async Task<ActionResult<List<Room>>> GetArchives()
        {
            var archives = await _roomStore.GetArchivedRoomsAsync();
            return Ok(archives);
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<Room>> GetArchivedRoom(string code)
        {
            var room = await _roomStore.GetArchivedRoomAsync(code);
            if (room == null) return NotFound();
            return Ok(room);
        }
    }
}
