using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;

namespace WorthBoards.Api.IntegrationTests.Infrastructure;

public class FakeEmailService : IEmailService
{
    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
