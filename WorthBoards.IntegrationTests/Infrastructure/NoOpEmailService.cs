using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;

namespace WorthBoards.IntegrationTests.Infrastructure;

internal sealed class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}