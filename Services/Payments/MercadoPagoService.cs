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

            // ✅ CRÍTICO: MercadoPagoConfig debe usar el token que empieza con "TEST-"
            // Actualmente en tus variables de Render tienes uno de producción "APP_USR-"
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") 
                ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        /// <summary>
        /// Crea una preferencia de pago optimizada para Sandbox.
        /// </summary>
        /// <param name="total">Monto total de la orden</param>
        /// <param name="orderId">ID de la orden en tu base de datos</param>
        /// <param name="emailComprador">Email del usuario de prueba (Comprador)</param>
        public async Task<string> CrearPago(decimal total, int orderId, string emailComprador)
        {
            var client = new PreferenceClient();

            // ✅ DETECCIÓN AUTOMÁTICA DE URL BASE PARA WEBHOOKS Y RETORNO
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
                // Usamos el email del usuario de prueba (ej. el que termina en 79833)
                Payer = new PreferencePayerRequest
                {
                    Email = emailComprador, 
                    Name = "Usuario",
                    Surname = "Prueba"
                },

                // ✅ CONFIGURACIÓN DE RETORNOS Y NOTIFICACIONES
                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                AutoReturn = "approved",
                
                // ✅ SOLUCIÓN AL ERROR "ESTAMOS REVISANDO TU PAGO":
                // Cambiamos a 'true' para que el resultado sea inmediato (Aprobado o Rechazado).
                BinaryMode = true, 
                
                ExternalReference = orderId.ToString(),

                StatementDescriptor = "MI TIENDA ECOMMERCE",

                // ✅ SOLUCIÓN AL ERROR "USA UN MEDIO DE PAGO DISTINTO":
                // Forzamos el uso de tarjetas de prueba excluyendo pagos en efectivo.
                PaymentMethods = new PreferencePaymentMethodsRequest
                {
                    ExcludedPaymentTypes = new List<PreferencePaymentTypeRequest>
                    {
                        new PreferencePaymentTypeRequest { Id = "ticket" }, // Excluye Efecty
                        new PreferencePaymentTypeRequest { Id = "atm" }
                    },
                    Installments = 1 
                }
            };

            // Creamos la preferencia en los servidores de Mercado Pago
            var result = await client.CreateAsync(preference);

            // ✅ RETORNAMOS SandboxInitPoint:
            // Es obligatorio usar este punto de inicio para que acepten las tarjetas de prueba.
            return result.SandboxInitPoint; 
        }
    }
}