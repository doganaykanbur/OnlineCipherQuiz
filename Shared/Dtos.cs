using System;
using System.Collections.Generic;

namespace CipherQuiz.Shared
{
    public class CreateRoomResult
    {
        public string RoomCode { get; set; } = string.Empty;
        public string AdminToken { get; set; } = string.Empty;
    }

    public class ParticipantDto
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public bool IsFinished { get; set; }
        public int CompletedQuestions { get; set; }
        public int TotalQuestions { get; set; }
        public int ProctorWarnings { get; set; }
    }

    public class ScoreboardEntryDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public double Score { get; set; }
        public bool IsFinished { get; set; }
        public int CurrentQuestionIndex { get; set; }
    }

    public class AnswerResultDto
    {
        public bool IsCorrect { get; set; }
        public double ScoreAwarded { get; set; }
        public double RemainingScore { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsFinished { get; set; }
    }

    public class ProctorEventInbound
    {
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
    }

    public class FinalResultsDto
    {
        public string RoomCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public int DurationSeconds { get; set; }
        public List<string> Topics { get; set; } = new();
        public List<ParticipantResultDto> Participants { get; set; } = new();
        public List<TopicStatDto> TopicStats { get; set; } = new();
    }

    public class ParticipantResultDto
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Completed { get; set; }
        public int Total { get; set; }
        public double Score { get; set; }
        public List<QuestionResultDetailDto> QuestionDetails { get; set; } = new();
    }

    public class QuestionResultDetailDto
    {
        public string Topic { get; set; } = string.Empty;
        public double Score { get; set; }
        public bool IsCorrect { get; set; }
        public int Attempts { get; set; }
    }

    public class TopicStatDto
    {
        public string Topic { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public int TotalWrongs { get; set; }
        public int TotalAttempts { get; set; }
        public string MostFrequentError { get; set; } = string.Empty;
    }
    
    public enum ResumeStatus
    {
        NotFound,
        Lobby,
        Running,
        Finished
    }
}
