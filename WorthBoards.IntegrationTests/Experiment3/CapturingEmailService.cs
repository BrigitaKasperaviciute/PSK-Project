using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;

namespace WorthBoards.IntegrationTests.Experiment3;

public sealed class CapturingEmailService : IEmailService
{
    private readonly List<SendEmailRequest> _sentEmails = new List<SendEmailRequest>();
    private readonly object _lock = new();

    public IReadOnlyList<SendEmailRequest> SentEmails
    {
        get
        {
            lock (_lock)
            {
                return _sentEmails.ToList();
            }
        }
    }

    public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _sentEmails.Add(sendEmailRequest);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _sentEmails.Clear();
        }
    }
}
