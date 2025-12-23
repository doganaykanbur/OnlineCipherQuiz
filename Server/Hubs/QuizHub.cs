using CipherQuiz.Server.Services;
using CipherQuiz.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace CipherQuiz.Server.Hubs
{
    public class QuizHub : Hub
    {
        private readonly IRoomStore _roomStore;
        private readonly IQuestionEngine _questionEngine;
        private readonly IConfiguration _configuration;

        public QuizHub(IRoomStore roomStore, IQuestionEngine questionEngine, IConfiguration configuration)
        {
            _roomStore = roomStore;
            _questionEngine = questionEngine;
            _configuration = configuration;
        }

        // --- Admin Methods ---

        public async Task<bool> ValidateMasterPassword(string password)
        {
            var masterPassword = _configuration["MasterPassword"] ?? "SiberVatan";
             return await Task.FromResult(password == masterPassword);
        }

        public async Task<CreateRoomResult> CreateRoom(string roomName, string password, string language = "tr", List<string>? customQuestionIds = null, bool isCryptanalysis = false)
        {
            var masterPassword = _configuration["MasterPassword"] ?? "SiberVatan";
            if (password != masterPassword)
            {
                throw new HubException("Invalid Password");
            }

            var room = new Room
            {
                Code = GenerateRoomCode(),
                Name = roomName,
                AdminName = "Yönetici", // Default name
                AdminToken = Guid.NewGuid().ToString(),
                State = RoomState.Lobby,
                Config = new QuizConfig 
                { 
                    Topics = new List<string> { "Caesar", "Vigenere" },
                    QuestionsPerTopicMap = new Dictionary<string, int> { { "Caesar", 2 }, { "Vigenere", 2 } },
                    Language = language,
                    CustomQuestionIds = customQuestionIds ?? new List<string>(),
                    IsCryptanalysis = isCryptanalysis
                }
            };

            await _roomStore.CreateRoomAsync(room);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.Code + "_Admin");
            await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
            
            return new CreateRoomResult { RoomCode = room.Code, AdminToken = room.AdminToken };
        }

        public async Task UpdateConfig(string roomCode, string adminToken, QuizConfig config)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            room.Config = config;
            await _roomStore.UpdateRoomAsync(room);
            await Clients.Group(roomCode + "_Admin").SendAsync("ConfigUpdated", config);
        }

        public async Task StartQuiz(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null || room.State != RoomState.Lobby) return;

            // Generate questions for each participant
            // Generate questions
            List<QuestionState>? masterQuestions = null;
            if (room.Config.SameQuestionsForEveryone)
            {
                // Generate once for everyone
                masterQuestions = await _questionEngine.BuildSet(room.Config, room.Config.Language);
            }

            foreach (var p in room.Participants.Where(x => x.IsApproved))
            {
                List<QuestionState> questions;
                if (masterQuestions != null)
                {
                    // Clone for each participant so they track their own state (Attempts, IsSolved)
                    questions = masterQuestions.Select(q => new QuestionState 
                    {
                        Id = Guid.NewGuid().ToString(), // Unique ID per participant instance to track stats individually? 
                        // Actually, tracking by QuestionId global is fine, but Solved state is inside QuestionState?
                        // Yes, QuestionState has 'IsSolved', 'UserAnswer'. We definitely need DEEP COPY.
                        Topic = q.Topic,
                        Prompt = q.Prompt,
                        InputHint = q.InputHint,
                        InputType = q.InputType,
                        Data = q.Data, // Dictionary ref is fine if read-only
                        CorrectAnswer = q.CorrectAnswer,
                        Position = q.Position,
                        Total = q.Total,
                        RemainingScore = q.RemainingScore, // Reset score logic
                        Attempts = 0
                    }).ToList();
                }
                else
                {
                    questions = await _questionEngine.BuildSet(room.Config, room.Config.Language);
                }

                var pState = new ParticipantQuizState
                {
                    ParticipantId = p.ParticipantId,
                    DisplayName = p.DisplayName,
                    Questions = questions,
                    CurrentIndex = 0,
                    Score = 0,
                    Language = room.Config.Language
                };
                room.Quiz[p.ParticipantId] = pState;
            }

            room.State = RoomState.Running;
            room.StartUtc = DateTime.UtcNow;
            await _roomStore.UpdateRoomAsync(room);

            await BroadcastScoreboard(room);
            await Clients.Group(roomCode).SendAsync("QuizStarted", room.StartUtc, room.Config);
        }

        public async Task FinishQuiz(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            room.State = RoomState.Finished;
            await _roomStore.UpdateRoomAsync(room);
            await _roomStore.ArchiveRoomAsync(room);
            await BroadcastScoreboard(room);
            await Clients.Group(roomCode).SendAsync("QuizFinished");
        }

        public async Task<RoomState?> ResumeAdmin(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return null;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode + "_Admin");
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await BroadcastParticipantList(room, Context.ConnectionId);
            await BroadcastScoreboard(room, Context.ConnectionId);
            return room.State;
        }

        public async Task<Room?> GetRoomInfo(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            return room;
        }

        public async Task Approve(string roomCode, string adminToken, string participantId)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            var p = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
            if (p != null)
            {
                p.IsApproved = true;
                await Clients.Client(p.ConnectionId).SendAsync("JoinApproved");
                await BroadcastParticipantList(room);
            }
        }

        public async Task Reject(string roomCode, string adminToken, string participantId)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            var p = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
            if (p != null)
            {
                room.Participants.Remove(p);
                await Clients.Client(p.ConnectionId).SendAsync("JoinRejected", "Admin rejected request.");
                await BroadcastParticipantList(room);
            }
        }

        public async Task CloseRoom(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            // Notify all participants
            await Clients.Group(roomCode).SendAsync("RoomClosed");
            
            // Archive before removing
            await _roomStore.ArchiveRoomAsync(room);

            // Remove from store
            await _roomStore.RemoveRoomAsync(roomCode);
        }

        public async Task ShowResults(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            await Clients.Group(roomCode).SendAsync("ShowResults");
        }

        public async Task ApproveAll(string roomCode, string adminToken)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            foreach (var p in room.Participants.Where(x => !x.IsApproved))
            {
                p.IsApproved = true;
                await Clients.Client(p.ConnectionId).SendAsync("JoinApproved");
            }
            await BroadcastParticipantList(room);
        }

        public async Task KickParticipant(string roomCode, string adminToken, string participantId)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return;

            var p = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
            if (p != null)
            {
                room.Participants.Remove(p);
                var msg = room.Config.Language == "en" ? "You have been kicked by the admin." : "Yönetici tarafından atıldınız.";
                await Clients.Client(p.ConnectionId).SendAsync("Kicked", msg);
                await BroadcastParticipantList(room);
            }
        }

        public async Task<List<QuestionState>> GetParticipantDetails(string roomCode, string adminToken, string participantId)
        {
            var room = await ValidateAdmin(roomCode, adminToken);
            if (room == null) return new List<QuestionState>();

            if (room.Quiz.TryGetValue(participantId, out var pState))
            {
                return pState.Questions;
            }
            return new List<QuestionState>();
        }

        // --- Participant Methods ---

        public async Task<JoinRoomResult> RequestJoin(string roomCode, string displayName, string? participantId = null)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null) return new JoinRoomResult { Success = false, Message = "Room not found" };

            // Check if exists
            if (!string.IsNullOrEmpty(participantId))
            {
                var existing = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
                if (existing != null)
                {
                    existing.ConnectionId = Context.ConnectionId;
                    existing.DisplayName = displayName; // Update name if changed
                    await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                    await _roomStore.UpdateRoomAsync(room);
                    return new JoinRoomResult 
                    { 
                        Success = true, 
                        Message = existing.ParticipantId, 
                        Language = room.Config.Language,
                        RoomName = room.Name 
                    };
                }
            }

            var p = new Participant
            {
                ParticipantId = !string.IsNullOrEmpty(participantId) ? participantId : Guid.NewGuid().ToString(),
                ConnectionId = Context.ConnectionId,
                DisplayName = displayName,
                IsApproved = false
            };

            room.Participants.Add(p);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await _roomStore.UpdateRoomAsync(room);

            // Notify Admin
            await Clients.Group(roomCode + "_Admin").SendAsync("ReceiveJoinRequest", Context.ConnectionId, displayName);
            await BroadcastParticipantList(room);

            return new JoinRoomResult 
            { 
                Success = true, 
                Message = p.ParticipantId,
                Language = room.Config.Language,
                RoomName = room.Name
            };
        }

        public async Task<ResumeStatus> ResumeParticipant(string roomCode, string participantId)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null) return ResumeStatus.NotFound;

            var p = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
            if (p == null) return ResumeStatus.NotFound;

            p.ConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            if (p.IsApproved)
            {
                // If running, send current state
                if (room.State == RoomState.Running)
                {
                    // Send current question if exists
                    // We don't send it here, client calls GetCurrentQuestion
                }
            }

            return (ResumeStatus)Enum.Parse(typeof(ResumeStatus), room.State.ToString());
        }

        public async Task<ParticipantQuizState?> GetState(string roomCode, string participantId)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null) return null;
            
            if (room.Quiz.TryGetValue(participantId, out var pState))
            {
                pState.StartUtc = room.StartUtc;
                pState.TimeLimitMinutes = room.Config.TimeLimitMinutes;
                pState.Language = room.Config.Language;
                pState.IsRoomFinished = room.State == RoomState.Finished;
                return pState;
            }
            return null;
        }


        public async Task<List<QuestionState>> GetQuestions(string roomCode, string participantId)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null || room.State != RoomState.Running) return new List<QuestionState>();

            if (room.Quiz.TryGetValue(participantId, out var pState))
            {
                // Return all questions without correct answer
                return pState.Questions.Select(q => new QuestionState 
                { 
                    Id = q.Id, 
                    Topic = q.Topic, 
                    Prompt = q.Prompt, 
                    InputHint = q.InputHint, 
                    InputType = q.InputType, 
                    Data = q.Data, 
                    Position = q.Position, 
                    Total = q.Total,
                    RemainingScore = q.RemainingScore,
                    Attempts = q.Attempts,
                    IsSolved = q.IsSolved
                }).ToList();
            }
            return new List<QuestionState>();
        }

        public async Task<AnswerResultDto> SubmitAnswer(string roomCode, string participantId, string questionId, string answer)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room == null) return new AnswerResultDto { Message = "Error" };

            if (room.Quiz.TryGetValue(participantId, out var pState))
            {
                var q = pState.Questions.FirstOrDefault(x => x.Id == questionId);
                if (q == null) return new AnswerResultDto { Message = "Question not found" };

                // Check if already solved or failed (max attempts reached)
                // We allow 2 mistakes (Attempts 0, 1). 3rd attempt (Attempt 2) is final.
                // Actually user said: "2 adet çarpı yeri... 2. hatadan sonra hata olursa... otomatikmen yanlış sayılıp diğer soruya atlancak"
                // So:
                // Attempt 1 (0 prev mistakes): Wrong -> X (1 mistake)
                // Attempt 2 (1 prev mistake): Wrong -> XX (2 mistakes)
                // Attempt 3 (2 prev mistakes): Wrong -> Fail question.
                
                if (q.RemainingScore <= 0 && q.Attempts >= 3) 
                     return new AnswerResultDto { Message = "Question already failed." };

                bool correct;
                var ans = (answer ?? "").Trim();
                var expected = q.CorrectAnswer.Trim();

                if (q.Topic == "Base64")
                {
                    // If expected answer has lower case letters, it's likely a Base64 string (case sensitive).
                    // If it's all upper, it's likely plaintext (case insensitive).
                    bool isBase64 = expected.Any(char.IsLower);
                    if (isBase64)
                        correct = string.Equals(expected, ans, StringComparison.Ordinal);
                    else
                        correct = string.Equals(expected, ans, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    correct = string.Equals(expected, ans, StringComparison.OrdinalIgnoreCase);
                }

                if (correct)
                {
                    pState.Score += q.RemainingScore;
                    q.UserAnswer = ans;
                    q.IsSolved = true; // Mark as solved
                    pState.CurrentIndex++; // Move to next question index logically for tracking
                    
                    bool finished = CheckIfFinished(pState);
                    if (finished)
                    {
                        var participant = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
                        if (participant != null)
                        {
                            participant.IsFinished = true;
                            await BroadcastParticipantList(room);
                        }
                    }

                    await BroadcastScoreboard(room);
                    await _roomStore.UpdateRoomAsync(room);
                    return new AnswerResultDto 
                    { 
                        IsCorrect = true, 
                        ScoreAwarded = q.RemainingScore, 
                        IsFinished = finished
                    };
                }
                else
                {
                    q.Attempts++;
                    // Removed penalty on first mistake as requested.
                    // if (q.Attempts == 1 && q.RemainingScore > 0) ...
                    // Logic: 2 strikes allowed. 2nd mistake is fatal.
                    if (q.Attempts >= 2)
                    {
                        q.RemainingScore = 0;
                        pState.CurrentIndex++; // Move past failed question
                        
                        bool finished = CheckIfFinished(pState);
                        if (finished)
                        {
                            var participant = room.Participants.FirstOrDefault(x => x.ParticipantId == participantId);
                            if (participant != null)
                            {
                                participant.IsFinished = true;
                                await BroadcastParticipantList(room);
                            }
                        }
                        await BroadcastScoreboard(room);
                        await _roomStore.UpdateRoomAsync(room);

                        return new AnswerResultDto 
                        { 
                            IsCorrect = false, 
                            RemainingScore = 0, 
                            Message = "2. Hata! Soru iptal oldu.",
                            IsFinished = finished 
                        };
                    }
                    
                    q.UserAnswer = ans; // Store last wrong answer
                    await _roomStore.UpdateRoomAsync(room);
                    return new AnswerResultDto 
                    { 
                        IsCorrect = false, 
                        RemainingScore = q.RemainingScore, 
                        Message = "Yanlış cevap." 
                    };
                }
            }
            return new AnswerResultDto { Message = "State not found" };
        }

        private bool CheckIfFinished(ParticipantQuizState pState)
        {
            // Simple check: are all questions either solved (IsSolved) or failed (Attempts >= 3)?
            return pState.Questions.All(q => q.IsSolved || q.Attempts >= 3);
        }

        public async Task ReportProctorEvent(string roomCode, string participantId, ProctorEventInbound ev)
        {
             var room = await _roomStore.GetRoomAsync(roomCode);
             if (room == null) return;

             if (!room.ProctorLogs.ContainsKey(participantId))
                 room.ProctorLogs[participantId] = new List<ProctorEvent>();

             var log = new ProctorEvent { Type = ev.Type, Content = ev.Content, TimestampUtc = ev.TimestampUtc };
             room.ProctorLogs[participantId].Add(log);

             await _roomStore.UpdateRoomAsync(room);

             // Notify admin
             await Clients.Group(roomCode + "_Admin").SendAsync("ProctorEvent", participantId, log);
        }

        // --- Helpers ---

        private async Task<Room?> ValidateAdmin(string roomCode, string token)
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room != null && room.AdminToken == token) return room;
            return null;
        }

        private string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[new Random().Next(s.Length)]).ToArray());
        }

        private async Task BroadcastParticipantList(Room room, string? connectionId = null)
        {
            var dtos = room.Participants.Select(p => new ParticipantDto
            {
                ParticipantId = p.ParticipantId,
                DisplayName = p.DisplayName,
                IsApproved = p.IsApproved,
                IsFinished = p.IsFinished
            }).ToList();

            if (connectionId is null)
                await Clients.Group(room.Code + "_Admin").SendAsync("ParticipantListChanged", dtos);
            else
                await Clients.Client(connectionId).SendAsync("ParticipantListChanged", dtos);
        }

        private async Task BroadcastScoreboard(Room room, string? connectionId = null)
        {
            var scores = room.Quiz.Values.Select(qs => new ScoreboardEntryDto
            {
                DisplayName = qs.DisplayName,
                Score = qs.Score,
                IsFinished = qs.CurrentIndex >= qs.Questions.Count || room.State == RoomState.Finished,
                CurrentQuestionIndex = qs.CurrentIndex + 1
            }).OrderByDescending(x => x.Score).ToList();

            if (connectionId is null)
                await Clients.Group(room.Code + "_Admin").SendAsync("ScoreboardUpdated", scores);
            else
                await Clients.Client(connectionId).SendAsync("ScoreboardUpdated", scores);
            // Also send to participants if desired, usually only at end or top 3
        }

        public async Task CheckTime(string roomCode) // Called periodically by client or admin
        {
            var room = await _roomStore.GetRoomAsync(roomCode);
            if (room != null && room.State == RoomState.Running && room.StartUtc.HasValue)
            {
                var elapsed = DateTime.UtcNow - room.StartUtc.Value;
                if (elapsed.TotalMinutes >= room.Config.TimeLimitMinutes)
                {
                    // Mark all active participants as finished
                    bool changed = false;
                    foreach(var p in room.Participants)
                    {
                        if (!p.IsFinished) 
                        {
                            p.IsFinished = true;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        await _roomStore.UpdateRoomAsync(room);
                        await BroadcastScoreboard(room);
                        await BroadcastParticipantList(room);
                    }
                }
            }
        }
    }
}
