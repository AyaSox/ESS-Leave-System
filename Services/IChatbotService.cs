using System.Security.Claims;

namespace ESSLeaveSystem.Services
{
    public record ChatbotAnswer(string Answer, IEnumerable<ChatAction> Actions);

    public record ChatAction(string Label, string Url, string Icon = "fas fa-link");

    public interface IChatbotService
    {
        Task<ChatbotAnswer> GetAnswerAsync(string message, ClaimsPrincipal user);
    }
}
