using CipherQuiz.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CipherQuiz.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResultsController : ControllerBase
    {
        private readonly IRoomStore _roomStore;
        private readonly ResultExportService _exportService;

        public ResultsController(IRoomStore roomStore, ResultExportService exportService)
        {
            _roomStore = roomStore;
            _exportService = exportService;
        }

        [HttpGet("{roomCode}/xlsx")]
        public async Task<IActionResult> GetXlsx(string roomCode, [FromQuery] string adminToken, [FromQuery] bool detailed = false)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null || room.AdminToken != adminToken) return Unauthorized();

            var data = _exportService.BuildFinalResults(room);
            var file = _exportService.GenerateXlsx(data, detailed);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Results_{roomCode}.xlsx");
        }

        [HttpGet("{roomCode}/pdf")]
        public async Task<IActionResult> GetPdf(string roomCode, [FromQuery] string adminToken, [FromQuery] bool detailed = false)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null || room.AdminToken != adminToken) return Unauthorized();

            var data = _exportService.BuildFinalResults(room);
            var file = _exportService.GeneratePdf(data, detailed);
            return File(file, "application/pdf", $"Results_{roomCode}.pdf");
        }
    }
}
