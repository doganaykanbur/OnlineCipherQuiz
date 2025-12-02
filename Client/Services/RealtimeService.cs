using CipherQuiz.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace CipherQuiz.Client.Services
{
    public class RealtimeService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly NavigationManager _navigationManager;

        private HubConnection HubConnection => _hubConnection ?? throw new InvalidOperationException("Realtime connection has not been started.");

        public event Action? OnJoinApproved;
        public event Action<string>? OnJoinRejected;
        public event Action<QuizConfig>? OnConfigUpdated;
        public event Action<DateTime, QuizConfig>? OnQuizStarted;
        public event Action? OnQuizFinished;
        public event Action<List<ParticipantDto>>? OnParticipantListChanged;
        public event Action<List<ScoreboardEntryDto>>? OnScoreboardUpdated;
        public event Action<string, ProctorEvent>? OnProctorEventReceived;
        public event Action? OnRoomClosed;
        public event Action<string>? OnKicked;
        public event Action? OnShowResults;

        public RealtimeService(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public async Task StartConnection()
        {
            if (_hubConnection is not null) return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("/quizhub"))
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("JoinApproved", () => OnJoinApproved?.Invoke());
            _hubConnection.On<string>("JoinRejected", (reason) => OnJoinRejected?.Invoke(reason));
            _hubConnection.On<QuizConfig>("ConfigUpdated", (config) => OnConfigUpdated?.Invoke(config));
            _hubConnection.On<DateTime, QuizConfig>("QuizStarted", (startUtc, config) => OnQuizStarted?.Invoke(startUtc, config));
            _hubConnection.On("QuizFinished", () => OnQuizFinished?.Invoke());
            _hubConnection.On<List<ParticipantDto>>("ParticipantListChanged", (list) => OnParticipantListChanged?.Invoke(list));
            _hubConnection.On<List<ScoreboardEntryDto>>("ScoreboardUpdated", (list) => OnScoreboardUpdated?.Invoke(list));
            _hubConnection.On<string, ProctorEvent>("ProctorEvent", (pid, ev) => OnProctorEventReceived?.Invoke(pid, ev));
            _hubConnection.On("RoomClosed", () => OnRoomClosed?.Invoke());
            _hubConnection.On<string>("Kicked", (reason) => OnKicked?.Invoke(reason));
            _hubConnection.On("ShowResults", () => OnShowResults?.Invoke());

            await _hubConnection.StartAsync();
        }

        // Admin Methods
        public async Task<CreateRoomResult> CreateRoom(string name, string adminName) 
            => await HubConnection.InvokeAsync<CreateRoomResult>("CreateRoom", name, adminName);

        public async Task UpdateConfig(string roomCode, string token, QuizConfig config)
            => await HubConnection.InvokeAsync("UpdateConfig", roomCode, token, config);

        public async Task StartQuiz(string roomCode, string token)
            => await HubConnection.InvokeAsync("StartQuiz", roomCode, token);

        public async Task FinishQuiz(string roomCode, string token)
            => await HubConnection.InvokeAsync("FinishQuiz", roomCode, token);
            
        public async Task Approve(string roomCode, string token, string pid)
            => await HubConnection.InvokeAsync("Approve", roomCode, token, pid);

        public async Task Reject(string roomCode, string token, string pid)
            => await HubConnection.InvokeAsync("Reject", roomCode, token, pid);

        public async Task<RoomState?> ResumeAdmin(string roomCode, string token)
            => await HubConnection.InvokeAsync<RoomState?>("ResumeAdmin", roomCode, token);

        public async Task CloseRoom(string roomCode, string token)
            => await HubConnection.InvokeAsync("CloseRoom", roomCode, token);

        public async Task ApproveAll(string roomCode, string token)
            => await HubConnection.InvokeAsync("ApproveAll", roomCode, token);

        public async Task KickParticipant(string roomCode, string token, string pid)
            => await HubConnection.InvokeAsync("KickParticipant", roomCode, token, pid);

        public async Task ShowResults(string roomCode, string token)
            => await HubConnection.InvokeAsync("ShowResults", roomCode, token);

        // Participant Methods
        public async Task<string> RequestJoin(string roomCode, string displayName)
            => await HubConnection.InvokeAsync<string>("RequestJoin", roomCode, displayName);

        public async Task<ResumeStatus> ResumeParticipant(string roomCode, string pid)
            => await HubConnection.InvokeAsync<ResumeStatus>("ResumeParticipant", roomCode, pid);

        public async Task<List<QuestionState>> GetQuestions(string roomCode, string pid)
            => await HubConnection.InvokeAsync<List<QuestionState>>("GetQuestions", roomCode, pid);

        public async Task<AnswerResultDto> SubmitAnswer(string roomCode, string pid, string questionId, string answer)
            => await HubConnection.InvokeAsync<AnswerResultDto>("SubmitAnswer", roomCode, pid, questionId, answer);

        public async Task ReportProctorEvent(string roomCode, string pid, ProctorEventInbound ev)
            => await HubConnection.InvokeAsync("ReportProctorEvent", roomCode, pid, ev);

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}
