using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Business.Utils.EmailService;

namespace WorthBoards.Api.IntegrationTests.TestDoubles;

public class TestEmailService : IEmailService
{
    public List<SendEmailRequest> SentEmails { get; } = new();

    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        SentEmails.Add(sendEmailRequest);
        return Task.CompletedTask;
    }
}
