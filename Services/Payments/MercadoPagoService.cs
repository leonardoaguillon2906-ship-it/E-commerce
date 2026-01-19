using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System; 

namespace EcommerceApp.Services.Payments
{
    public class MercadoPagoService
    {
        private readonly IConfiguration _configuration;

        public MercadoPagoService(IConfiguration configuration)
        {
            _configuration = configuration;

            // Intenta obtener el token de la variable de entorno directa o de la jerarquía del JSON
            string token = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") 
                           ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];

            MercadoPagoConfig.AccessToken = token;
        }

        public async Task<string> CrearPago(decimal total, int orderId)
        {
            var client = new PreferenceClient();

            // Detecta URL de Render o usa la de Ngrok para pruebas locales
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
                Payer = new PreferencePayerRequest
                {
                    Email = "test_user_123456@testuser.com" // Email ficticio para Sandbox
                },
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },
                AutoReturn = "approved",
                BinaryMode = true, // Evita estados pendientes
                ExternalReference = orderId.ToString()
            };

            var result = await client.CreateAsync(preference);
            return result.SandboxInitPoint; 
        }
    }
}