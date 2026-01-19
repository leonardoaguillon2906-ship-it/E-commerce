using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Linq; // Agregado para el Where
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
        // MÉTODO REQUERIDO POR ASP.NET IDENTITY (Confirmación cuenta, etc)
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

            // ✅ CARGA DEL TEMPLATE
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
        // MÉTODO PARA EL WEBHOOK (Utilizado por MercadoPagoWebhookController)
        // =====================================================
        public async Task SendAsync(string to, string subject, string body)
        {
            await EnviarCorreoAsync(to, subject, body);
        }

        // =====================================================
        // MÉTODO PRIVADO PRINCIPAL DE ENVÍO SMTP
        // =====================================================
        private async Task EnviarCorreoAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to))
                return;

            var smtpHost = _config["EmailSettings:SmtpServer"];
            var smtpPort = _config["EmailSettings:Port"];
            var username = _config["EmailSettings:Username"];
            var password = _config["EmailSettings:Password"];
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderName = _config["EmailSettings:SenderName"];

            // Validación de configuración
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("[Email Error] Configuración SMTP incompleta en variables de entorno.");
                return;
            }

            try 
            {
                using var smtp = new SmtpClient
                {
                    Host = smtpHost,
                    Port = int.TryParse(smtpPort, out var port) ? port : 587,
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
            catch (Exception ex)
            {
                // Evitamos que un fallo de correo tire abajo el Webhook (Error 500)
                Console.WriteLine($"[Email Exception] {ex.Message}");
            }
        }
    }
}