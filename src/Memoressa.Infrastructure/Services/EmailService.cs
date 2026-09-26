using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
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
    private readonly AwsS3Options _s3Options;

    public EmailService(
        ILogger<EmailService> logger,
        IHostEnvironment environment,
        IOptions<AwsSesOptions> sesOptions,
        IOptions<AwsS3Options> s3Options)
    {
        _logger = logger;
        _environment = environment;
        _sesOptions = sesOptions.Value;
        _s3Options = s3Options.Value;
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
        if (_environment.IsDevelopment() && string.IsNullOrWhiteSpace(_sesOptions.FromEmail))
        {
            _logger.LogInformation("DEV password reset OTP for {Email}. Code: {Code}", email, code);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sesOptions.FromEmail))
        {
            _logger.LogWarning("AwsSes:FromEmail is not configured; cannot send OTP to {Email}", email);
            throw new InvalidOperationException("Email is not configured");
        }

        var subject = "Memoressa 密碼重設驗證碼";
        var textBody =
            $"您的 Memoressa 密碼重設驗證碼為：{code}\n\n" +
            $"此驗證碼 {PasswordResetCodeRules.TtlMinutes} 分鐘內有效。如非本人操作，請忽略此信。\n";
        var htmlBody =
            $"<p>您的 Memoressa 密碼重設驗證碼為：<strong>{WebUtility.HtmlEncode(code)}</strong></p>" +
            $"<p>此驗證碼 {PasswordResetCodeRules.TtlMinutes} 分鐘內有效。如非本人操作，請忽略此信。</p>";

        using var client = CreateSesClient();
        var from = string.IsNullOrWhiteSpace(_sesOptions.FromDisplayName)
            ? _sesOptions.FromEmail
            : $"{_sesOptions.FromDisplayName} <{_sesOptions.FromEmail}>";

        var request = new SendEmailRequest
        {
            Source = from,
            Destination = new Destination { ToAddresses = [email] },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Text = new Content(textBody),
                    Html = new Content(htmlBody)
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_sesOptions.ConfigurationSetName))
        {
            request.ConfigurationSetName = _sesOptions.ConfigurationSetName;
        }

        await client.SendEmailAsync(request, cancellationToken);
        _logger.LogInformation("Password reset OTP email sent to {Email} via SES", email);
    }

    private IAmazonSimpleEmailService CreateSesClient()
    {
        var region = RegionEndpoint.GetBySystemName(_sesOptions.Region);
        var accessKey = !string.IsNullOrWhiteSpace(_sesOptions.AccessKey)
            ? _sesOptions.AccessKey
            : _s3Options.AccessKey;
        var secretKey = !string.IsNullOrWhiteSpace(_sesOptions.SecretKey)
            ? _sesOptions.SecretKey
            : _s3Options.SecretKey;

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            return new AmazonSimpleEmailServiceClient(new BasicAWSCredentials(accessKey, secretKey), region);
        }

        return new AmazonSimpleEmailServiceClient(region);
    }
}
