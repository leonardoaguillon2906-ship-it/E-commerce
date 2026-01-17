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

        public async Task<string> CrearPago(decimal total, int orderId)
        {
            var client = new PreferenceClient();

            // ✅ DETECCIÓN AUTOMÁTICA DE URL BASE
            // Si existe la variable en Render la usa, sino usa ngrok o localhost para pruebas locales
            string baseUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") 
                ?? "https://spectrohelioscopic-porpoiselike-wilber.ngrok-free.dev"; 

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

            var preference = new PreferenceRequest
            {
                Items = items,

                // ✅ WEBHOOK DINÁMICO
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                // ✅ BACKURLS DINÁMICAS (Arregla el error ERR_NGROK_3200)
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                AutoReturn = "approved",
                ExternalReference = orderId.ToString()
            };

            var result = await client.CreateAsync(preference);

            // Cambiar a result.InitPoint cuando pases a producción (producción usa links reales)
            return result.SandboxInitPoint; 
        }
    }
}