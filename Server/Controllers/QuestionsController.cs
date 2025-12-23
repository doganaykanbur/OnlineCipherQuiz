using CipherQuiz.Server.Services;
using CipherQuiz.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CipherQuiz.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionsController : ControllerBase
    {
        private readonly IQuestionEngine _engine;

        public QuestionsController(IQuestionEngine engine)
        {
            _engine = engine;
        }

        [HttpGet("practice")]
        public async Task<ActionResult<List<QuestionState>>> GetPracticeSet([FromQuery] int count = 5, [FromQuery] string language = "tr")
        {
            var map = new Dictionary<string, int>
            {
                { "Caesar", count / 2 },
                { "Vigenere", count - (count / 2) }
            };
            var config = new QuizConfig
            {
                QuestionsPerTopicMap = map,
                MistakesPerQuestion = 3,
                Language = language
            };
            return await _engine.BuildSet(config, language);
        }
    }
}
