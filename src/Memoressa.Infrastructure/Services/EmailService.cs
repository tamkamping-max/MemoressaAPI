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
            _logger.LogWarning("Email FromEmail is not configured (AwsSes:FromEmail); cannot send OTP to {Email}", email);
            throw new InvalidOperationException("Email is not configured");
        }

        if (!IsSmtpConfigured())
        {
            _logger.LogWarning("SMTP is not configured (AwsSes:SmtpHost, SmtpUsername, SmtpPassword)");
            throw new InvalidOperationException("Email SMTP is not configured");
        }

        var subject = "Memoressa password reset verification code";
        var ttl = PasswordResetCodeRules.TtlMinutes;
        var textBody =
            $"Your Memoressa password reset verification code is: {code}\n\n" +
            $"This code is valid for {ttl} minutes. If you did not request a reset, you can ignore this email.\n";
        var htmlBody =
            $"<p>Your Memoressa password reset verification code is: <strong>{WebUtility.HtmlEncode(code)}</strong></p>" +
            $"<p>This code is valid for {ttl} minutes. If you did not request a reset, you can ignore this email.</p>";

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
        _logger.LogInformation(
            "OTP email accepted by SMTP for {Email} (host={SmtpHost}, from={FromEmail}). " +
            "If the inbox is empty: check spam; confirm From matches your provider rules (SES verification, Gmail App Password, Workspace relay allowlist, etc.).",
            email,
            ResolveSmtpHost(),
            _sesOptions.FromEmail.Trim());
    }

    public Task SendEmailVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default) =>
        SendOtpEmailAsync(
            email,
            code,
            "Memoressa email verification code",
            "Your Memoressa email verification code is:",
            cancellationToken);

    public Task SendEmailChangeCodeAsync(string email, string code, CancellationToken cancellationToken = default) =>
        SendOtpEmailAsync(
            email,
            code,
            "Memoressa email change verification code",
            "Your Memoressa email change verification code is:",
            cancellationToken);

    private async Task SendOtpEmailAsync(
        string email,
        string code,
        string subject,
        string introLine,
        CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment() && !IsSmtpConfigured())
        {
            _logger.LogInformation("DEV OTP for {Email} ({Subject}). Code: {Code}", email, subject, code);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sesOptions.FromEmail))
        {
            _logger.LogWarning("Email FromEmail is not configured (AwsSes:FromEmail); cannot send OTP to {Email}", email);
            throw new InvalidOperationException("Email is not configured");
        }

        if (!IsSmtpConfigured())
        {
            _logger.LogWarning("SMTP is not configured (AwsSes:SmtpHost, SmtpUsername, SmtpPassword)");
            throw new InvalidOperationException("Email SMTP is not configured");
        }

        var ttl = PasswordResetCodeRules.TtlMinutes;
        var textBody =
            $"{introLine} {code}\n\n" +
            $"This code is valid for {ttl} minutes. If you did not request this, you can ignore this email.\n";
        var htmlBody =
            $"<p>{WebUtility.HtmlEncode(introLine)} <strong>{WebUtility.HtmlEncode(code)}</strong></p>" +
            $"<p>This code is valid for {ttl} minutes. If you did not request this, you can ignore this email.</p>";

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

        // Default host is AWS SES SMTP; Google/Gmail/others must set AwsSes:SmtpHost explicitly.
        var region = string.IsNullOrWhiteSpace(_sesOptions.Region) ? "us-east-1" : _sesOptions.Region.Trim();
        return $"email-smtp.{region}.amazonaws.com";
    }
}
