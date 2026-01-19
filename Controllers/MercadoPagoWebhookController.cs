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
    // ✅ RUTA FINAL: Tu URL en Mercado Pago debe ser: https://tu-app.onrender.com/webhook-mp
    [Route("webhook-mp")] 
    [IgnoreAntiforgeryToken]
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

            // ✅ CONFIGURACIÓN GLOBAL DE TOKEN (Cargado desde variables de entorno de Render)
            var accessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN");
            if (!string.IsNullOrEmpty(accessToken))
            {
                MercadoPagoConfig.AccessToken = accessToken;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            try 
            {
                // 1. Capturar el cuerpo de la notificación
                string body;
                using (var reader = new StreamReader(Request.Body))
                {
                    body = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(body)) return Ok();

                using var jsonDoc = JsonDocument.Parse(body);
                var payload = jsonDoc.RootElement;

                // 2. Identificar tipo de evento
                if (!payload.TryGetProperty("type", out var typeProp)) return Ok();
                var type = typeProp.GetString();

                long paymentId = 0;

                if (type == "payment")
                {
                    if (payload.TryGetProperty("data", out var dataProp) &&
                        dataProp.TryGetProperty("id", out var idProp))
                    {
                        string idStr = idProp.ValueKind == JsonValueKind.Number ? 
                                       idProp.GetInt64().ToString() : idProp.GetString();

                        // ✅ CLAVE: Evitar error 500 con el ID de prueba de Mercado Pago
                        if (idStr == "123456") return Ok(new { message = "Test exitoso" });

                        paymentId = long.Parse(idStr);
                    }
                }
                else if (type == "merchant_order" || type == "topic_merchant_order_wh")
                {
                    // Manejo opcional de Merchant Orders para mayor robustez
                    long merchantOrderId = 0;
                    if (payload.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var mid))
                        merchantOrderId = ReadLong(mid);
                    else if (payload.TryGetProperty("resource", out var res))
                        merchantOrderId = ReadLong(res);

                    if (merchantOrderId > 0)
                    {
                        var moClient = new MerchantOrderClient();
                        var mo = await moClient.GetAsync(merchantOrderId);
                        var approved = mo.Payments?.FirstOrDefault(p => p.Status == "approved");
                        if (approved != null) paymentId = approved.Id ?? 0;
                    }
                }

                if (paymentId == 0) return Ok();

                // 3. Consultar API de Mercado Pago y procesar orden
                var paymentClient = new PaymentClient();
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
                // Logueamos pero devolvemos 200 para que MP no reintente fallos de lógica
                Console.WriteLine($"[Webhook Error] {ex.Message}");
            }

            return Ok(); 
        }

        private async Task ProcesarOrden(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status != "Pagado")
            {
                // Descontar Stock
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.Stock = Math.Max(0, item.Product.Stock - item.Quantity);
                    }
                }

                order.Status = "Pagado";
                await _context.SaveChangesAsync();
                
                // Enviar confirmación por Email
                try 
                {
                    var bodyHtml = await _templateService.BuildOrderConfirmationEmail(order);
                    await _emailService.SendAsync(order.User.Email, $"¡Pago Confirmado! Pedido #{order.Id}", bodyHtml);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"[Email Error] No se pudo enviar el correo: {ex.Message}");
                }
            }
        }

        private long ReadLong(JsonElement el) => 
            el.ValueKind == JsonValueKind.Number ? el.GetInt64() : long.Parse(el.GetString() ?? "0");
    }
}