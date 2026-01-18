using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using EcommerceApp.Models;

namespace EcommerceApp.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IWebHostEnvironment _env;

        public EmailTemplateService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // =====================================================
        // MÉTODO EXISTENTE (NO SE MODIFICA)
        // =====================================================
        public async Task<string> LoadAsync(string templateName)
        {
            var path = Path.Combine(
                _env.ContentRootPath,
                "EmailTemplates",
                templateName
            );

            return await File.ReadAllTextAsync(path);
        }

        // =====================================================
        // MÉTODO REQUERIDO POR IEmailTemplateService
        // =====================================================
        public async Task<string> BuildOrderConfirmationEmail(Order order)
        {
            var template = await LoadAsync("OrderConfirmation.html");

            template = template
                .Replace("{{OrderId}}", order.Id.ToString())
                .Replace("{{Total}}", order.Total.ToString("N2"))
                .Replace("{{Date}}", order.CreatedAt.ToString("dd/MM/yyyy"));

            return template;
        }
    }
}
