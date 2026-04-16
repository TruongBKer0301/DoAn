using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace LapTopBD.Utilities
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> emailSettings, ILogger<SmtpEmailSender> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost)
                || string.IsNullOrWhiteSpace(_emailSettings.SenderEmail)
                || string.IsNullOrWhiteSpace(_emailSettings.SenderPassword))
            {
                _logger.LogWarning("Email settings are not fully configured. Skip sending email to {Email}.", toEmail);
                return new EmailSendResult(false, "Thiếu cấu hình SMTP (SmtpHost/SenderEmail/SenderPassword).");
            }

            try
            {
                using var smtp = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
                {
                    EnableSsl = _emailSettings.UseSsl,
                    Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword)
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);
                await smtp.SendMailAsync(message);
                return new EmailSendResult(true);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP failed to send email to {Email}", toEmail);
                return new EmailSendResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                return new EmailSendResult(false, ex.Message);
            }
        }
    }
}
