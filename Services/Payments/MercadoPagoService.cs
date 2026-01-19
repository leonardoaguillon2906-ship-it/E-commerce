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
            // El AccessToken debe ser el del VENDEDOR (tu cuenta de desarrollador)
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") 
                ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        /// <summary>
        /// Crea una preferencia de pago dinámica.
        /// </summary>
        /// <param name="total">Monto total de la orden</param>
        /// <param name="orderId">ID de la orden en tu base de datos</param>
        /// <param name="emailComprador">Email del usuario de prueba que está comprando</param>
        public async Task<string> CrearPago(decimal total, int orderId, string emailComprador)
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

                // ✅ PAGADOR DINÁMICO: 
                // Usamos el email que llega por parámetro (el del usuario logueado)
                Payer = new PreferencePayerRequest
                {
                    Email = emailComprador, 
                    Name = "Usuario",
                    Surname = "Prueba"
                },

                // ✅ WEBHOOK Y BACKURLS
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                // ✅ CONFIGURACIONES DE FLUJO
                AutoReturn = "approved",
                
                // Mantenemos BinaryMode en false para evitar rechazos automáticos por riesgo en Sandbox
                BinaryMode = false, 
                
                ExternalReference = orderId.ToString(),

                StatementDescriptor = "MI TIENDA ECOMMERCE",

                // ✅ OPCIONAL: Restringir a tarjetas para asegurar éxito en Sandbox
                PaymentMethods = new PreferencePaymentMethodsRequest
                {
                    ExcludedPaymentTypes = new List<PreferencePaymentTypeRequest>
                    {
                        new PreferencePaymentTypeRequest { Id = "ticket" }, // Excluye Efecty/Bancos
                        new PreferencePaymentTypeRequest { Id = "atm" }
                    },
                    Installments = 1 
                }
            };

            // Ejecuta la petición a Mercado Pago
            var result = await client.CreateAsync(preference);

            // Importante: Retornamos SandboxInitPoint para el entorno de pruebas
            return result.SandboxInitPoint; 
        }
    }
}