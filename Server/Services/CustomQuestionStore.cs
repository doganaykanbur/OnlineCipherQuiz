using System.Text.Json;
using CipherQuiz.Shared;

namespace CipherQuiz.Server.Services
{
    public class CustomQuestionStore
    {
        private readonly string _filePath;

        public CustomQuestionStore()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomQuestions.json");
            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, "[]");
            }
        }

        public async Task<List<CustomQuestion>> GetQuestionsAsync()
        {
            if (!File.Exists(_filePath)) return new List<CustomQuestion>();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<CustomQuestion>>(json) ?? new List<CustomQuestion>();
        }

        public async Task AddQuestionAsync(CustomQuestion question)
        {
            var questions = await GetQuestionsAsync();
            questions.Add(question);
            await SaveAsync(questions);
        }

        public async Task DeleteQuestionAsync(string id)
        {
            var questions = await GetQuestionsAsync();
            var q = questions.FirstOrDefault(x => x.Id == id);
            if (q != null)
            {
                questions.Remove(q);
                await SaveAsync(questions);
            }
        }

        private async Task SaveAsync(List<CustomQuestion> questions)
        {
            var json = JsonSerializer.Serialize(questions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}
