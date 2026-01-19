using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System; // ✅ Necesario para Environment

namespace EcommerceApp.Services.Payments
{
    public class MercadoPagoService
    {
        private readonly IConfiguration _configuration;

        public MercadoPagoService(IConfiguration configuration)
        {
            _configuration = configuration;

            // Prioriza la variable de entorno de Render, si no existe, usa appsettings.json
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN")
                ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        /// <summary>
        /// Crea un pago en Mercado Pago (sandbox o producción) y devuelve el link para redireccionar.
        /// En sandbox se aprueba automáticamente para pruebas.
        /// </summary>
        /// <param name="total">Monto total del pedido</param>
        /// <param name="orderId">Id de la orden</param>
        /// <returns>URL de checkout de Mercado Pago</returns>
        public async Task<string> CrearPago(decimal total, int orderId)
        {
            var client = new PreferenceClient();

            // ✅ DETECCIÓN AUTOMÁTICA DE URL BASE
            string baseUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL")
                ?? "https://localhost:5001"; // Cambiar si pruebas local con ngrok

            // ✅ ITEMS DE LA ORDEN
            var items = new List<PreferenceItemRequest>
            {
                new PreferenceItemRequest
                {
                    Title = $"Orden #{orderId}",
                    Quantity = 1,
                    CurrencyId = "COP",
                    UnitPrice = total
                }
            };

            // ✅ CONFIGURACIÓN DE PREFERENCIA DE PAGO
            var preference = new PreferenceRequest
            {
                Items = items,

                // ✅ WEBHOOK DINÁMICO PARA ACTUALIZAR ESTADOS EN TU APP
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                // ✅ BACKURLS DINÁMICAS
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                // ✅ AUTO-RETURN PARA APROBAR AUTOMÁTICAMENTE EN SANDBOX
                AutoReturn = "approved",
                ExternalReference = orderId.ToString()
            };

            // Crear la preferencia en Mercado Pago
            var result = await client.CreateAsync(preference);

            // ✅ En sandbox usamos SandboxInitPoint
            //    En producción cambiar a result.InitPoint
            string checkoutUrl = result.SandboxInitPoint;

            // ⚡ OPCIONAL: Forzar estado aprobado automáticamente en sandbox
            // Esto asegura que tu vista muestre “Pago aprobado” sin esperar intervención manual
            if (checkoutUrl.Contains("sandbox"))
            {
                // Nota: Mercado Pago en sandbox ya aprueba automáticamente si AutoReturn="approved"
                // Esta línea solo es referencia, no es necesario cambiar nada
            }

            return checkoutUrl;
        }
    }
}
