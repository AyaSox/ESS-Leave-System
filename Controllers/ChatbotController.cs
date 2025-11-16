using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESSLeaveSystem.Services;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;

namespace ESSLeaveSystem.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbot;
        private readonly LeaveDbContext _context;
        public ChatbotController(IChatbotService chatbot, LeaveDbContext context)
        {
            _chatbot = chatbot;
            _context = context;
        }

        public record ChatRequest(string Message);
        public record ChatResponse(string Answer, IEnumerable<ChatAction> Actions);

        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
        {
            var res = await _chatbot.GetAnswerAsync(request.Message, User);

            // log query
            var log = new ChatbotQueryLog
            {
                Email = User?.Identity?.Name,
                Question = request.Message ?? string.Empty,
                Answer = res.Answer,
                CreatedDate = DateTime.UtcNow
            };
            _context.ChatbotQueryLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new ChatResponse(res.Answer, res.Actions));
        }
    }
}
