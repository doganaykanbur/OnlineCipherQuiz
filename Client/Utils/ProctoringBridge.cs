using CipherQuiz.Client.Services;
using CipherQuiz.Shared;
using Microsoft.JSInterop;

namespace CipherQuiz.Client.Utils
{
    public class ProctoringBridge
    {
        private readonly RealtimeService _service;
        private readonly string _roomCode;
        private readonly string _participantId;

        public ProctoringBridge(RealtimeService service, string roomCode, string participantId)
        {
            _service = service;
            _roomCode = roomCode;
            _participantId = participantId;
        }

        [JSInvokable]
        public async Task OnProctorEvent(ProctorEventInbound ev)
        {
            await _service.ReportProctorEvent(_roomCode, _participantId, ev);
        }
    }
}
