using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using System.Collections.Concurrent;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class FakeEmailService : IEmailService
{
    private static readonly ConcurrentDictionary<string, SendEmailRequest> LastEmailByRecipient = new(StringComparer.OrdinalIgnoreCase);

    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        LastEmailByRecipient[sendEmailRequest.RecipientEmail] = sendEmailRequest;
        return Task.CompletedTask;
    }

    public static bool TryGetLastEmail(string recipientEmail, out SendEmailRequest sendEmailRequest)
    {
        return LastEmailByRecipient.TryGetValue(recipientEmail, out sendEmailRequest!);
    }
}
