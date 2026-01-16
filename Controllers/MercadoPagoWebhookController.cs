using EcommerceApp.Data;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercadoPago.Resource.Payment;
using MercadoPago.Resource.MerchantOrder;
using MercadoPago.Client.Payment;
using MercadoPago.Client.MerchantOrder;
using System.Text.Json;

namespace EcommerceApp.Controllers
{
    [ApiController]
    [Route("api/mercadopago")]
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
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Receive()
        {
            string body;

            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                Console.WriteLine("[Webhook] Body vacío");
                return Ok();
            }

            JsonElement payload;

            try
            {
                payload = JsonDocument.Parse(body).RootElement;
            }
            catch
            {
                Console.WriteLine("[Webhook] JSON inválido");
                return Ok();
            }

            if (!payload.TryGetProperty("type", out var typeProp))
            {
                Console.WriteLine("[Webhook] Sin type");
                return Ok();
            }

            var type = typeProp.GetString();
            Console.WriteLine($"[Webhook] Tipo: {type}");

            long paymentId = 0;

            // ============================
            // EVENTO PAYMENT
            // ============================
            if (type == "payment")
            {
                if (payload.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("id", out var idProp))
                {
                    paymentId = ReadLong(idProp);
                }
                else
                {
                    Console.WriteLine("[Webhook] Sin data.id en payment");
                    return Ok();
                }
            }
            // ============================
            // EVENTO MERCHANT ORDER
            // ============================
            else if (type == "merchant_order" || type == "topic_merchant_order_wh")
            {
                long merchantOrderId = 0;

                if (payload.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("id", out var moIdProp))
                {
                    merchantOrderId = ReadLong(moIdProp);
                }
                else if (payload.TryGetProperty("resource", out var resourceProp))
                {
                    merchantOrderId = ReadLong(resourceProp);
                }
                else if (payload.TryGetProperty("id", out var rootIdProp))
                {
                    merchantOrderId = ReadLong(rootIdProp);
                }

                if (merchantOrderId == 0)
                {
                    Console.WriteLine("[Webhook] No se pudo obtener merchant_order id");
                    return Ok();
                }

                Console.WriteLine($"[Webhook] MerchantOrderId: {merchantOrderId}");

                var moClient = new MerchantOrderClient();
                MerchantOrder merchantOrder;

                try
                {
                    merchantOrder = await moClient.GetAsync(merchantOrderId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Webhook] Error consultando merchant order: {ex.Message}");
                    return Ok();
                }

                var approvedPayment = merchantOrder.Payments?
                    .FirstOrDefault(p => p.Status == "approved");

                if (approvedPayment == null || !approvedPayment.Id.HasValue)
                {
                    Console.WriteLine("[Webhook] Sin pagos aprobados en merchant order");
                    return Ok();
                }

                paymentId = approvedPayment.Id.Value;
            }
            else
            {
                Console.WriteLine($"[Webhook] Tipo ignorado: {type}");
                return Ok();
            }

            Console.WriteLine($"[Webhook] PaymentId: {paymentId}");

            // ============================
            // CONSULTAR PAGO
            // ============================
            var paymentClient = new PaymentClient();
            Payment payment;

            try
            {
                payment = await paymentClient.GetAsync(paymentId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Webhook] Error consultando pago: {ex.Message}");
                return Ok();
            }

            Console.WriteLine($"[Webhook] Estado pago: {payment.Status}");

            if (payment.Status != "approved")
                return Ok();

            if (string.IsNullOrEmpty(payment.ExternalReference) ||
                !int.TryParse(payment.ExternalReference, out int orderId))
            {
                Console.WriteLine($"[Webhook] ExternalReference inválido: {payment.ExternalReference}");
                return Ok();
            }

            Console.WriteLine($"[Webhook] OrderId: {orderId}");

            // ============================
            // ACTUALIZAR ORDEN Y STOCK
            // ============================
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                Console.WriteLine($"[Webhook] Orden no encontrada: {orderId}");
                return Ok();
            }

            if (order.Status == "Pagado")
            {
                Console.WriteLine("[Webhook] Orden ya procesada");
                return Ok();
            }

            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.Stock -= item.Quantity;
                    if (item.Product.Stock < 0)
                        item.Product.Stock = 0;

                    _context.Products.Update(item.Product);
                }
            }

            order.Status = "Pagado";
            _context.Orders.Update(order);

            await _context.SaveChangesAsync();
            Console.WriteLine($"[Webhook] Orden {order.Id} procesada correctamente");

            // ============================
            // ENVÍO DE CORREO (PLANTILLA)
            // ============================
            if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
            {
                var template = await _templateService.LoadAsync("OrderPaid.html");

                var itemsHtml = string.Join("", order.OrderItems.Select(i =>
                    $"<li>{i.Product.Name} x{i.Quantity}</li>"
                ));

                var bodyHtml = template
                    .Replace("{{CustomerName}}", order.User.Email)
                    .Replace("{{OrderId}}", order.Id.ToString())
                    .Replace("{{OrderItems}}", itemsHtml)
                    .Replace("{{Total}}", order.Total.ToString("N0"))
                    .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

                await _emailService.SendAsync(
                    order.User.Email,
                    $"Confirmación de pago - Orden #{order.Id}",
                    bodyHtml
                );

                Console.WriteLine("[Webhook] Correo enviado correctamente");
            }

            return Ok();
        }

        private long ReadLong(JsonElement el)
        {
            try
            {
                return el.ValueKind == JsonValueKind.Number
                    ? el.GetInt64()
                    : long.Parse(el.GetString());
            }
            catch
            {
                return 0;
            }
        }
    }
}
