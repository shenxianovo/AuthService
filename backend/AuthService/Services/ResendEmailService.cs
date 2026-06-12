using AuthService.Configuration;
using Microsoft.Extensions.Options;
using Resend;

namespace AuthService.Services
{
    public class ResendEmailService(IResend resend, IOptions<ResendOptions> options) : IEmailService
    {
        private readonly ResendOptions _options = options.Value;

        public async Task SendVerificationCodeAsync(string toEmail, string displayName, string code)
        {
            var message = new EmailMessage();
            message.From = $"{_options.FromName} <{_options.FromEmail}>";
            message.To.Add(toEmail);
            message.Subject = "Your verification code";
            message.HtmlBody = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 40px;">
                  <div style="max-width: 480px; margin: 0 auto; background: #fff; border-radius: 8px; padding: 32px; box-shadow: 0 2px 8px rgba(0,0,0,0.08);">
                    <h2 style="color: #333; margin-top: 0;">Email Verification</h2>
                    <p style="color: #555;">Hi {displayName},</p>
                    <p style="color: #555;">Use the code below to verify your email address. This code expires in {_options.VerificationCodeExpirationMinutes} minutes.</p>
                    <div style="font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #4f46e5; text-align: center; padding: 24px 0;">
                      {code}
                    </div>
                    <p style="color: #888; font-size: 13px;">If you did not request this, you can safely ignore this email.</p>
                  </div>
                </body>
                </html>
                """;

            await resend.EmailSendAsync(message);
        }

        public async Task SendPasswordResetLinkAsync(string toEmail, string displayName, string resetUrl)
        {
            var message = new EmailMessage();
            message.From = $"{_options.FromName} <{_options.FromEmail}>";
            message.To.Add(toEmail);
            message.Subject = "Reset your password";
            message.HtmlBody = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 40px;">
                  <div style="max-width: 480px; margin: 0 auto; background: #fff; border-radius: 8px; padding: 32px; box-shadow: 0 2px 8px rgba(0,0,0,0.08);">
                    <h2 style="color: #333; margin-top: 0;">Password Reset</h2>
                    <p style="color: #555;">Hi {displayName},</p>
                    <p style="color: #555;">Click the button below to reset your password. This link expires in {_options.PasswordResetExpirationMinutes} minutes and can be used once.</p>
                    <div style="text-align: center; padding: 24px 0;">
                      <a href="{resetUrl}" style="background: #4f46e5; color: #fff; text-decoration: none; padding: 12px 32px; border-radius: 6px; font-weight: bold; display: inline-block;">Reset Password</a>
                    </div>
                    <p style="color: #888; font-size: 13px;">If you did not request this, you can safely ignore this email — your password will not change.</p>
                  </div>
                </body>
                </html>
                """;

            await resend.EmailSendAsync(message);
        }
    }
}
