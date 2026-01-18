using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    /// <summary>
    /// Servicio de envío de correos.
    /// Implementa IEmailSender para compatibilidad con ASP.NET Identity.
    /// </summary>
    public class EmailService : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        // =====================================================
        // MÉTODO REQUERIDO POR ASP.NET IDENTITY
        // (ForgotPassword, ConfirmEmail, etc.)
        // =====================================================
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await EnviarCorreoAsync(email, subject, htmlMessage);
        }

        // =====================================================
        // MÉTODO PRINCIPAL DE TU APLICACIÓN
        // =====================================================
        public async Task EnviarCorreoAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("El destinatario no puede estar vacío", nameof(to));

            var smtpHost = _config["EmailSettings:SmtpServer"];
            var smtpPort = _config["EmailSettings:Port"];
            var username = _config["EmailSettings:Username"];
            var password = _config["EmailSettings:Password"];
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderName = _config["EmailSettings:SenderName"];

            if (string.IsNullOrEmpty(smtpHost) ||
                string.IsNullOrEmpty(smtpPort) ||
                string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(senderEmail))
            {
                throw new InvalidOperationException(
                    "La configuración EmailSettings es inválida o incompleta");
            }

            using var smtp = new SmtpClient
            {
                Host = smtpHost,
                Port = int.Parse(smtpPort),
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(to);

            await smtp.SendMailAsync(mail);
        }

        // =====================================================
        // MÉTODO FACHADA / COMPATIBILIDAD INTERNA
        // =====================================================
        public async Task SendAsync(string to, string subject, string body)
        {
            await EnviarCorreoAsync(to, subject, body);
        }
    }
}
