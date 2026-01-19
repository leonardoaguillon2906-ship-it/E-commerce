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
    // ✅ RUTA SIMPLIFICADA: Evita el prefijo /api/ para reducir errores 404 en el Webhook
    [Route("mercadopago")] 
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

            // ✅ CONFIGURACIÓN DE TOKEN: Sincroniza con la variable 'MERCADOPAGO_ACCESS_TOKEN' de Render
            var accessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN");
            if (!string.IsNullOrEmpty(accessToken))
            {
                MercadoPagoConfig.AccessToken = accessToken;
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Receive()
        {
            // Captura del cuerpo de la notificación
            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body)) return Ok();

            JsonElement payload;
            try
            {
                payload = JsonDocument.Parse(body).RootElement;
            }
            catch
            {
                return Ok(); // Retornamos 200 para que MP no reintente payloads corruptos
            }

            // Validar que el payload contenga un tipo de evento
            if (!payload.TryGetProperty("type", out var typeProp)) return Ok();

            var type = typeProp.GetString();
            long paymentId = 0;

            // 1. OBTENER EL ID DEL PAGO (Soporta eventos 'payment' y 'merchant_order')
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
                long merchantOrderId = 0;
                if (payload.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("id", out var moIdProp))
                    merchantOrderId = ReadLong(moIdProp);
                else if (payload.TryGetProperty("resource", out var resourceProp))
                    merchantOrderId = ReadLong(resourceProp);
                else if (payload.TryGetProperty("id", out var rootIdProp))
                    merchantOrderId = ReadLong(rootIdProp);

                if (merchantOrderId > 0)
                {
                    var moClient = new MerchantOrderClient();
                    var merchantOrder = await moClient.GetAsync(merchantOrderId);
                    var approvedPayment = merchantOrder.Payments?.FirstOrDefault(p => p.Status == "approved");
                    if (approvedPayment != null) paymentId = approvedPayment.Id ?? 0;
                }
            }

            if (paymentId == 0) return Ok();

            // 2. CONSULTAR EL ESTADO REAL EN MERCADO PAGO
            var paymentClient = new PaymentClient();
            Payment payment;
            try
            {
                payment = await paymentClient.GetAsync(paymentId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Webhook Error MP API] {ex.Message}");
                return Ok();
            }

            // 3. VALIDACIÓN DE ESTADO Y REFERENCIA
            if (payment.Status != "approved") return Ok();

            if (string.IsNullOrEmpty(payment.ExternalReference) ||
                !int.TryParse(payment.ExternalReference, out int orderId))
            {
                return Ok();
            }

            // 4. ACTUALIZACIÓN DE BASE DE DATOS (Atomicidad)
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // Si la orden no existe o ya fue pagada, terminamos con éxito
            if (order == null || order.Status == "Pagado") return Ok();

            // Descuento de Stock seguro
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    // Evitamos que el stock sea menor a cero
                    product.Stock = Math.Max(0, product.Stock - item.Quantity);
                    _context.Products.Update(product);
                }
            }

            order.Status = "Pagado";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            // 5. NOTIFICACIÓN AL CLIENTE (Email)
            if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
            {
                try 
                {
                    var template = await _templateService.LoadAsync("OrderPaid.html");
                    var itemsHtml = string.Join("", order.OrderItems.Select(i =>
                        $"<li>{i.Product?.Name} (x{i.Quantity}) - ${i.Price:N0}</li>"));

                    var bodyHtml = template
                        .Replace("{{CustomerName}}", order.User.Email)
                        .Replace("{{OrderId}}", order.Id.ToString())
                        .Replace("{{OrderItems}}", itemsHtml)
                        .Replace("{{Total}}", order.Total.ToString("N0"))
                        .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

                    await _emailService.SendAsync(order.User.Email, $"¡Pago Confirmado! Orden #{order.Id}", bodyHtml);
                    Console.WriteLine($"[Webhook Success] Email enviado a {order.User.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Webhook Email Error] {ex.Message}");
                }
            }

            return Ok(); // Notificamos a MP que recibimos el mensaje correctamente
        }

        private long ReadLong(JsonElement el)
        {
            try {
                return el.ValueKind == JsonValueKind.Number ? el.GetInt64() : long.Parse(el.GetString() ?? "0");
            } catch { return 0; }
        }
    }
}