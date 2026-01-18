using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using EcommerceApp.Models;
using EcommerceApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services
{
    /// <summary>
    /// Servicio de envío de correos.
    /// Implementa IEmailSender (Identity) e IEmailService (aplicación).
    /// </summary>
    public class EmailService : IEmailSender, IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly IEmailTemplateService _emailTemplateService;

        public EmailService(
            IConfiguration config,
            ApplicationDbContext context,
            IEmailTemplateService emailTemplateService)
        {
            _config = config;
            _context = context;
            _emailTemplateService = emailTemplateService;
        }

        // =====================================================
        // MÉTODO REQUERIDO POR ASP.NET IDENTITY
        // =====================================================
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await EnviarCorreoAsync(email, subject, htmlMessage);
        }

        // =====================================================
        // MÉTODO REQUERIDO POR IEmailService (COMPRA)
        // =====================================================
        public async Task SendOrderConfirmationEmail(Order order)
        {
            if (order == null)
                return;

            var userEmail = await _context.Users
                .Where(u => u.Id == order.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userEmail))
                return;

            // ✅ CARGA CORRECTA DEL TEMPLATE
            var template = await _emailTemplateService.LoadAsync("OrderSuccess.html");

            // ✅ REEMPLAZO DE VARIABLES
            var body = template
                .Replace("{{ORDER_ID}}", order.Id.ToString())
                .Replace("{{TOTAL}}", order.Total.ToString("C"))
                .Replace("{{STATUS}}", order.Status)
                .Replace("{{DATE}}", order.CreatedAt.ToString("dd/MM/yyyy"));

            await EnviarCorreoAsync(
                userEmail,
                "Confirmación de tu compra",
                body
            );
        }

        // =====================================================
        // MÉTODO PRINCIPAL DE ENVÍO SMTP (NO SE TOCA)
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
        // MÉTODO FACHADA / USO INTERNO (SE CONSERVA)
        // =====================================================
        public async Task SendAsync(string to, string subject, string body)
        {
            await EnviarCorreoAsync(to, subject, body);
        }
    }
}
