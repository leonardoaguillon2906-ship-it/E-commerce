using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercadoPago.Resource.Payment;
using MercadoPago.Client.Payment;
using MercadoPago.Client.MerchantOrder;
using MercadoPago.Config;
using System.Text.Json;

namespace EcommerceApp.Controllers
{
    [ApiController]
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

            // ✅ Carga el token de acceso desde Render
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
                // 1. Leer el cuerpo de la notificación
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(body)) return Ok();

                using var jsonDoc = JsonDocument.Parse(body);
                var payload = jsonDoc.RootElement;

                // 2. Identificar tipo de evento (Payment o Merchant Order)
                if (!payload.TryGetProperty("type", out var typeProp)) return Ok();
                var type = typeProp.GetString();

                long paymentId = 0;

                if (type == "payment")
                {
                    if (payload.TryGetProperty("data", out var dataProp) &&
                        dataProp.TryGetProperty("id", out var idProp))
                    {
                        // ✅ Manejo flexible: el ID puede venir como string o número
                        string idStr = idProp.ValueKind == JsonValueKind.Number ? 
                                       idProp.GetInt64().ToString() : idProp.GetString();

                        // ✅ SOLUCIÓN AL ERROR 500: Si es el ID de prueba de Mercado Pago, responder 200 inmediatamente
                        if (idStr == "123456" || string.IsNullOrEmpty(idStr)) 
                            return Ok(new { message = "Test notification received successfully" });

                        if (long.TryParse(idStr, out long parsedId)) paymentId = parsedId;
                    }
                }
                else if (type == "merchant_order")
                {
                    // Algunos webhooks envían la orden del comerciante en lugar del pago directo
                    if (payload.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var mid))
                    {
                        long merchantOrderId = ReadLong(mid);
                        if (merchantOrderId > 0)
                        {
                            var moClient = new MerchantOrderClient();
                            var mo = await moClient.GetAsync(merchantOrderId);
                            // Buscamos el último pago aprobado dentro de la orden
                            var approved = mo.Payments?.FirstOrDefault(p => p.Status == "approved");
                            if (approved != null) paymentId = approved.Id ?? 0;
                        }
                    }
                }

                // 3. Si encontramos un ID de pago válido, procesar la lógica de negocio
                if (paymentId > 0)
                {
                    var paymentClient = new PaymentClient();
                    var payment = await paymentClient.GetAsync(paymentId);
                    
                    if (payment.Status == "approved" && !string.IsNullOrEmpty(payment.ExternalReference))
                    {
                        if (int.TryParse(payment.ExternalReference, out int orderId))
                        {
                            await ProcesarActualizacionOrden(orderId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Importante: No devolver Error 500 para evitar bucles de reintentos de Mercado Pago
                Console.WriteLine($"[Webhook Critical Error] {DateTime.UtcNow}: {ex.Message}");
            }

            return Ok(); 
        }

        private async Task ProcesarActualizacionOrden(int orderId)
        {
            // Usamos un contexto limpio para evitar problemas de rastreo
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // Solo procesamos si la orden existe y aún no está marcada como pagada
            if (order != null && order.Status != "Pagado")
            {
                // ✅ 1. Actualizar Stock de productos
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.Stock = Math.Max(0, item.Product.Stock - item.Quantity);
                    }
                }

                // ✅ 2. Cambiar estado de la orden
                order.Status = "Pagado";
                await _context.SaveChangesAsync();
                
                // ✅ 3. Enviar correo electrónico (dentro de try-catch para no afectar la DB)
                try 
                {
                    var bodyHtml = await _templateService.BuildOrderConfirmationEmail(order);
                    await _emailService.SendAsync(order.User.Email, $"Confirmación de Pago - Pedido #{order.Id}", bodyHtml);
                }
                catch (Exception emailEx) 
                {
                    Console.WriteLine($"[Email Notification Error] Orden #{orderId}: {emailEx.Message}");
                }
            }
        }

        private long ReadLong(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number) return el.GetInt64();
            if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out long val)) return val;
            return 0;
        }
    }
}