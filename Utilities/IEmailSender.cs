namespace LapTopBD.Utilities
{
    public interface IEmailSender
    {
        Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody);
    }

    public record EmailSendResult(bool Success, string? ErrorMessage = null);
}
