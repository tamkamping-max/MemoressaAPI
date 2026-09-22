using Memoressa.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Memoressa.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;

    public EmailService(ILogger<EmailService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "DEV password reset for {Email}. Token: {Token}",
                email,
                resetToken);
        }
        else
        {
            _logger.LogInformation("Password reset email queued for {Email}", email);
        }

        return Task.CompletedTask;
    }
}
