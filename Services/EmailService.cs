using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using EcommerceApp.Models; // ✅ Necesario para reconocer el objeto Order

namespace EcommerceApp.Services
{
    // ✅ Línea 36 corregida: Ahora implementa ambas interfaces
    public class EmailService : IEmailSender, IEmailService 
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        // =====================================================
        // ✅ MÉTODO PARA EL CHECKOUT (Implementa IEmailService)
        // =====================================================
        public async Task SendOrderConfirmationEmail(Order order)
        {
            if (order?.User == null) return;

            var subject = $"Confirmación de Pedido #{order.Id}";
            var body = $@"
                <html>
                    <body>
                        <h2 style='color: #d35400;'>¡Gracias por tu compra!</h2>
                        <p>Hola {order.User.FullName}, hemos recibido tu pedido.</p>
                        <p><strong>Total:</strong> {order.Total:C}</p>
                        <p>Tu pedido será procesado a la brevedad.</p>
                    </body>
                </html>";

            await EnviarCorreoAsync(order.User.Email, subject, body);
        }

        // =====================================================
        // MÉTODOS DE IDENTIDAD (IEmailSender)
        // =====================================================
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await EnviarCorreoAsync(email, subject, htmlMessage);
        }

        // =====================================================
        // LÓGICA SMTP ORIGINAL (Sin borrar nada)
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

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPort) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(senderEmail))
            {
                throw new InvalidOperationException("Configuración de Email incompleta");
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

        public async Task SendAsync(string to, string subject, string body)
        {
            await EnviarCorreoAsync(to, subject, body);
        }
    }
}