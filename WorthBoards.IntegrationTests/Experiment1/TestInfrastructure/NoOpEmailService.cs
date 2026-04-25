using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;

namespace WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;

public class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
