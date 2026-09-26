using System.Net;
using System.Net.Mail;
using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly AwsSesOptions _sesOptions;

    public EmailService(
        ILogger<EmailService> logger,
        IHostEnvironment environment,
        IOptions<AwsSesOptions> sesOptions)
    {
        _logger = logger;
        _environment = environment;
        _sesOptions = sesOptions.Value;
    }

    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("DEV legacy password reset link for {Email}. Token: {Token}", email, resetToken);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Legacy password reset email queued for {Email}", email);
        return Task.CompletedTask;
    }

    public async Task SendPasswordResetCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment() && !IsSmtpConfigured())
        {
            _logger.LogInformation("DEV password reset OTP for {Email}. Code: {Code}", email, code);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sesOptions.FromEmail))
        {
            _logger.LogWarning("AwsSes:FromEmail is not configured; cannot send OTP to {Email}", email);
            throw new InvalidOperationException("Email is not configured");
        }

        if (!IsSmtpConfigured())
        {
            _logger.LogWarning("AwsSes SMTP (host/username/password) is not configured");
            throw new InvalidOperationException("Email SMTP is not configured");
        }

        var subject = "Memoressa 密碼重設驗證碼";
        var textBody =
            $"您的 Memoressa 密碼重設驗證碼為：{code}\n\n" +
            $"此驗證碼 {PasswordResetCodeRules.TtlMinutes} 分鐘內有效。如非本人操作，請忽略此信。\n";
        var htmlBody =
            $"<p>您的 Memoressa 密碼重設驗證碼為：<strong>{WebUtility.HtmlEncode(code)}</strong></p>" +
            $"<p>此驗證碼 {PasswordResetCodeRules.TtlMinutes} 分鐘內有效。如非本人操作，請忽略此信。</p>";

        var fromAddress = new MailAddress(
            _sesOptions.FromEmail.Trim(),
            string.IsNullOrWhiteSpace(_sesOptions.FromDisplayName)
                ? "Memoressa"
                : _sesOptions.FromDisplayName.Trim());

        using var message = new MailMessage
        {
            From = fromAddress,
            Subject = subject,
            Body = textBody,
            IsBodyHtml = false
        };
        message.To.Add(email.Trim());
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        if (!string.IsNullOrWhiteSpace(_sesOptions.ConfigurationSetName))
        {
            message.Headers.Add("X-SES-CONFIGURATION-SET", _sesOptions.ConfigurationSetName.Trim());
        }

        using var client = new SmtpClient(ResolveSmtpHost(), _sesOptions.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(
                _sesOptions.SmtpUsername!.Trim(),
                _sesOptions.SmtpPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Password reset OTP email sent to {Email} via SES SMTP", email);
    }

    private bool IsSmtpConfigured() =>
        !string.IsNullOrWhiteSpace(_sesOptions.SmtpUsername)
        && !string.IsNullOrWhiteSpace(_sesOptions.SmtpPassword)
        && !string.IsNullOrWhiteSpace(ResolveSmtpHost());

    private string ResolveSmtpHost()
    {
        if (!string.IsNullOrWhiteSpace(_sesOptions.SmtpHost))
        {
            return _sesOptions.SmtpHost.Trim();
        }

        var region = string.IsNullOrWhiteSpace(_sesOptions.Region) ? "us-east-1" : _sesOptions.Region.Trim();
        return $"email-smtp.{region}.amazonaws.com";
    }
}
