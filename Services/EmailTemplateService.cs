using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using EcommerceApp.Models;
using System.Linq;
using System;

namespace EcommerceApp.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IWebHostEnvironment _env;

        public EmailTemplateService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> LoadAsync(string templateName)
        {
            // ✅ Ajuste para Render: Asegura que la carpeta y el archivo existan
            // Se recomienda que la carpeta sea "EmailTemplates" (con E y T mayúsculas)
            var path = Path.Combine(
                _env.ContentRootPath, 
                "EmailTemplates", 
                templateName
            );

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"La plantilla {templateName} no se encuentra en {path}");
            }

            return await File.ReadAllTextAsync(path);
        }

        public async Task<string> BuildOrderConfirmationEmail(Order order)
        {
            // ✅ Cargamos la plantilla de confirmación de pago
            var template = await LoadAsync("OrderPaid.html");

            // Generar el HTML de los productos dinámicamente
            var itemsHtml = string.Empty;
            if (order.OrderItems != null)
            {
                itemsHtml = string.Join("", order.OrderItems.Select(i =>
                    $"<tr>" +
                    $"<td style='padding: 10px; border-bottom: 1px solid #eee;'>{i.Product?.Name ?? "Producto"}</td>" +
                    $"<td style='padding: 10px; border-bottom: 1px solid #eee; text-align: center;'>{i.Quantity}</td>" +
                    $"<td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>${i.Price:N0}</td>" +
                    $"</tr>"));
            }

            // Reemplazo de variables en la plantilla
            template = template
                .Replace("{{CustomerName}}", order.User?.Email ?? "Cliente")
                .Replace("{{OrderId}}", order.Id.ToString())
                .Replace("{{OrderItems}}", itemsHtml)
                .Replace("{{Total}}", order.Total.ToString("N0"))
                .Replace("{{Date}}", order.CreatedAt.ToString("dd/MM/yyyy HH:mm"))
                .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

            return template;
        }
    }
}