using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class FakeEmailService : IEmailService
{
    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
