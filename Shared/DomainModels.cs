using System;
using System.Collections.Generic;

namespace CipherQuiz.Shared
{
    public class QuizConfig
    {
        public int DurationSeconds { get; set; } = 300;
        public int TimeLimitMinutes { get; set; } = 20; // Default 20 mins
        public int QuestionsPerTopic { get; set; } = 2;
        public List<string> Topics { get; set; } = new();
        public Dictionary<string, int> QuestionsPerTopicMap { get; set; } = new();
        public int MistakesPerQuestion { get; set; } = 3;
        public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard
    }

    public enum RoomState
    {
        Lobby,
        Running,
        Finished
    }

    public class Room
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AdminToken { get; set; } = string.Empty;

        public QuizConfig Config { get; set; } = new();
        public RoomState State { get; set; } = RoomState.Lobby;

        public DateTime? StartUtc { get; set; }

        public List<Participant> Participants { get; set; } = new();

        // ParticipantId -> ParticipantQuizState
        public Dictionary<string, ParticipantQuizState> Quiz { get; set; } = new();

        // ParticipantId -> Proctor events
        public Dictionary<string, List<ProctorEvent>> ProctorLogs { get; set; } = new();
    }

    public class Participant
    {
        public string ParticipantId { get; set; } = Guid.NewGuid().ToString();
        public string ConnectionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public bool IsFinished { get; set; }
    }

    public class ParticipantQuizState
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public List<QuestionState> Questions { get; set; } = new();

        public int CurrentIndex { get; set; }

        public double Score { get; set; } 

        public int Total => Questions.Count;
        public int Completed => Math.Min(CurrentIndex, Total);
    }

    public class QuestionState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Topic { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string InputHint { get; set; } = string.Empty;
        public string InputType { get; set; } = "text"; // text, number

        public Dictionary<string, string>? Data { get; set; }

        // Server-side only (should be nulled before sending to client if strictly secure, 
        // but for simplicity we might send it masked or handle in DTO mapping. 
        // Ideally, we don't send this to client.)
        public string CorrectAnswer { get; set; } = string.Empty;

        public int Attempts { get; set; }
        public double RemainingScore { get; set; }

        public int Position { get; set; }
        public int Total { get; set; }
    }

    public class ProctorEvent
    {
        public string Type { get; set; } = string.Empty; // copy, paste, blur, etc.
        public string Content { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
    }
}
