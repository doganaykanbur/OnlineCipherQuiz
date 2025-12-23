using CipherQuiz.Server.Services;
using CipherQuiz.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CipherQuiz.Server.Controllers
{
    [ApiController]
    [Route("api/custom-questions")]
    public class CustomQuestionController : ControllerBase
    {
        private readonly CustomQuestionStore _store;

        public CustomQuestionController(CustomQuestionStore store)
        {
            _store = store;
        }

        [HttpGet]
        public async Task<ActionResult<List<CustomQuestion>>> Get()
        {
            return await _store.GetQuestionsAsync();
        }

        [HttpPost]
        public async Task<ActionResult> Add([FromBody] CustomQuestion question)
        {
            await _store.AddQuestionAsync(question);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _store.DeleteQuestionAsync(id);
            return Ok();
        }
    }
}
