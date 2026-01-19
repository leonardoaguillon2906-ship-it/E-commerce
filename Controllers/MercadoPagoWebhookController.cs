using EcommerceApp.Data;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercadoPago.Resource.Payment;
using MercadoPago.Resource.MerchantOrder;
using MercadoPago.Client.Payment;
using MercadoPago.Client.MerchantOrder;
using MercadoPago.Config;
using System.Text.Json;

namespace EcommerceApp.Controllers
{
    [ApiController]
    // ✅ CAMBIO CLAVE: Ruta ultra simple para evitar el 404
    [Route("webhook-mp")] 
    public class MercadoPagoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly EmailTemplateService _templateService;

        public MercadoPagoWebhookController(
            ApplicationDbContext context,
            EmailService emailService,
            EmailTemplateService templateService)
        {
            _context = context;
            _emailService = emailService;
            _templateService = templateService;

            var accessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN");
            if (!string.IsNullOrEmpty(accessToken))
            {
                MercadoPagoConfig.AccessToken = accessToken;
            }
        }

        [HttpPost] // ✅ Escucha directamente en /webhook-mp
        public async Task<IActionResult> Receive()
        {
            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body)) return Ok();

            using var jsonDoc = JsonDocument.Parse(body);
            var payload = jsonDoc.RootElement;

            if (!payload.TryGetProperty("type", out var typeProp)) return Ok();
            var type = typeProp.GetString();

            long paymentId = 0;

            if (type == "payment")
            {
                if (payload.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("id", out var idProp))
                {
                    paymentId = ReadLong(idProp);
                }
            }
            else if (type == "merchant_order" || type == "topic_merchant_order_wh")
            {
                // ... (mismo código de búsqueda de MerchantOrder que ya tienes)
            }

            if (paymentId == 0) return Ok();

            var paymentClient = new PaymentClient();
            try 
            {
                var payment = await paymentClient.GetAsync(paymentId);
                
                if (payment.Status == "approved" && !string.IsNullOrEmpty(payment.ExternalReference))
                {
                    if (int.TryParse(payment.ExternalReference, out int orderId))
                    {
                        await ProcesarOrden(orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }

            return Ok(); // Mercado Pago dejará de marcar fallo al recibir este 200 OK
        }

        private async Task ProcesarOrden(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status != "Pagado")
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.Stock = Math.Max(0, item.Product.Stock - item.Quantity);
                    }
                }
                order.Status = "Pagado";
                await _context.SaveChangesAsync();
                
                // Enviar Email...
                var bodyHtml = await _templateService.BuildOrderConfirmationEmail(order);
                await _emailService.SendAsync(order.User.Email, $"Pago Exitoso #{order.Id}", bodyHtml);
            }
        }

        private long ReadLong(JsonElement el) => 
            el.ValueKind == JsonValueKind.Number ? el.GetInt64() : long.Parse(el.GetString() ?? "0");
    }
}