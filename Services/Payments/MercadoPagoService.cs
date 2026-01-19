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

            // ✅ DETECCIÓN AUTOMÁTICA DE URL BASE (Se mantiene igual)
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

                // ✅ MEJORA DEL PAGADOR: Agregamos nombre y apellido para que el filtro de seguridad lo valide mejor
                Payer = new PreferencePayerRequest
                {
                    Email = "test_user_1305459341@testuser.com", // Asegúrate de NO usar tu correo de cuenta real de Mercado Pago
                    Name = "Usuario",
                    Surname = "De Prueba"
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
                
                // Cambiamos BinaryMode a false temporalmente si el error persiste, 
                // ya que true obliga a un resultado inmediato que a veces falla en Sandbox.
                BinaryMode = false, 
                
                ExternalReference = orderId.ToString(),

                // ✅ AGREGAMOS TRACKS DE SEGURIDAD (Opcional pero recomendado para evitar rechazos)
                StatementDescriptor = "MI TIENDA ECOMMERCE"
            };

            // Creamos la preferencia
            var result = await client.CreateAsync(preference);

            // Importante: SandboxInitPoint es el correcto para pruebas.
            return result.SandboxInitPoint; 
        }
    }
}