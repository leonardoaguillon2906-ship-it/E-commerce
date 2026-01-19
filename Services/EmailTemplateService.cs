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
            // ✅ En Linux (Render), "EmailTemplates" != "emailtemplates". 
            // Aseguramos que la ruta sea absoluta y correcta.
            var path = Path.Combine(
                _env.ContentRootPath, 
                "EmailTemplates", 
                templateName
            );

            if (!File.Exists(path))
            {
                // En lugar de romper la ejecución con una excepción fatal, 
                // lanzamos una que capturaremos en BuildOrderConfirmationEmail
                throw new FileNotFoundException($"Template no encontrado: {path}");
            }

            return await File.ReadAllTextAsync(path);
        }

        public async Task<string> BuildOrderConfirmationEmail(Order order)
        {
            try 
            {
                // ✅ Intentamos cargar la plantilla principal
                var template = await LoadAsync("OrderPaid.html");

                // Generar el HTML de los productos dinámicamente
                var itemsHtml = string.Empty;
                if (order.OrderItems != null && order.OrderItems.Any())
                {
                    itemsHtml = string.Join("", order.OrderItems.Select(i =>
                        $"<tr>" +
                        $"<td style='padding: 10px; border-bottom: 1px solid #eee;'>{i.Product?.Name ?? "Producto"}</td>" +
                        $"<td style='padding: 10px; border-bottom: 1px solid #eee; text-align: center;'>{i.Quantity}</td>" +
                        $"<td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>${i.Price:N0}</td>" +
                        $"</tr>"));
                }
                else 
                {
                    itemsHtml = "<tr><td colspan='3' style='text-align:center; padding:10px;'>Detalles no disponibles</td></tr>";
                }

                // Reemplazo de variables
                template = template
                    .Replace("{{CustomerName}}", order.User?.Email ?? "Cliente")
                    .Replace("{{OrderId}}", order.Id.ToString())
                    .Replace("{{OrderItems}}", itemsHtml)
                    .Replace("{{Total}}", order.Total.ToString("N0"))
                    .Replace("{{Date}}", order.CreatedAt.ToString("dd/MM/yyyy HH:mm"))
                    .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

                return template;
            }
            catch (Exception ex)
            {
                // ✅ DISEÑO DE EMERGENCIA: Si falla la carga del archivo, enviamos un HTML básico
                // Esto garantiza que el Webhook responda 200 OK y no 500.
                Console.WriteLine($"[Template Error] Usando fallback: {ex.Message}");
                
                return $@"
                    <div style='font-family: sans-serif; padding: 20px; border: 1px solid #ddd;'>
                        <h2>¡Gracias por tu compra!</h2>
                        <p>Tu pago para la Orden #{order.Id} ha sido confirmado.</p>
                        <p>Total pagado: ${order.Total:N0}</p>
                        <p>Fecha: {order.CreatedAt:dd/MM/yyyy}</p>
                        <hr>
                        <p>Estamos preparando tu pedido.</p>
                    </div>";
            }
        }
    }
}