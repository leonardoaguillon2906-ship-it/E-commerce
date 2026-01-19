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

            // Prioriza la variable de entorno de Render, si no existe, usa appsettings.json
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") 
                ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        public async Task<string> CrearPago(decimal total, int orderId)
        {
            var client = new PreferenceClient();

            // ✅ DETECCIÓN AUTOMÁTICA DE URL BASE
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

                // ✅ AGREGAR PAGADOR DE PRUEBA
                // Esto evita que el sistema sospeche de fraude al intentar usar tu propia cuenta de desarrollador
                Payer = new PreferencePayerRequest
                {
                    Email = "test_user_123456@testuser.com" 
                },

                // ✅ WEBHOOK DINÁMICO
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                // ✅ BACKURLS DINÁMICAS
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                // ✅ CONFIGURACIONES CRÍTICAS PARA SANDBOX
                AutoReturn = "approved",
                BinaryMode = true, // Fuerza a que el resultado sea solo 'approved' o 'rejected'
                ExternalReference = orderId.ToString()
            };

            var result = await client.CreateAsync(preference);

            // Importante: SandboxInitPoint es exclusivo para pruebas con credenciales de prueba
            return result.SandboxInitPoint; 
        }
    }
}