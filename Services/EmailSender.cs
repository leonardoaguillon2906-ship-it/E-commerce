using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    /// <summary>
    /// Implementación básica de IEmailSender.
    /// Esta clase NO envía correos realmente.
    /// Se utiliza solo como implementación temporal o de prueba.
    /// </summary>
    public class EmailSender : IEmailSender
    {
        /// <summary>
        /// Método requerido por ASP.NET Identity.
        /// Actualmente no realiza ningún envío real.
        /// </summary>
        /// <param name="email">Correo destino</param>
        /// <param name="subject">Asunto</param>
        /// <param name="htmlMessage">Contenido HTML</param>
        /// <returns>Task completada</returns>
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // IMPLEMENTACIÓN TEMPORAL / PLACEHOLDER
            // No se conecta a SMTP ni a ningún proveedor real
            return Task.CompletedTask;
        }
    }
}
