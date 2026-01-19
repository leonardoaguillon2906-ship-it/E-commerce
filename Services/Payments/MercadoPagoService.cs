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

            // ✅ Carga el token desde Render. 
            // Si el token empieza con APP_USR-, el sistema lo tratará como producción a menos que usemos SandboxInitPoint.
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") 
                ?? _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        public async Task<string> CrearPago(decimal total, int orderId, string emailComprador)
        {
            var client = new PreferenceClient();

            // ✅ URL Base dinámica para Render
            string baseUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") 
                ?? "https://e-commerce-2-qmwg.onrender.com"; 

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

                // ✅ PAGADOR: Usamos el email que recibimos, pero el servicio lo valida
                Payer = new PreferencePayerRequest
                {
                    Email = emailComprador, 
                    Name = "Usuario",
                    Surname = "Prueba"
                },

                NotificationUrl = $"{baseUrl}/Cliente/Checkout/Webhook",

                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Cliente/Checkout/Success",
                    Failure = $"{baseUrl}/Cliente/Checkout/Failure",
                    Pending = $"{baseUrl}/Cliente/Checkout/Pending"
                },

                AutoReturn = "approved",
                
                // ✅ BinaryMode en true para aprobación inmediata
                BinaryMode = true, 
                
                ExternalReference = orderId.ToString(),

                StatementDescriptor = "MI TIENDA ECOMMERCE",

                PaymentMethods = new PreferencePaymentMethodsRequest
                {
                    ExcludedPaymentTypes = new List<PreferencePaymentTypeRequest>
                    {
                        new PreferencePaymentTypeRequest { Id = "ticket" }, 
                        new PreferencePaymentTypeRequest { Id = "atm" }
                    },
                    Installments = 1 
                }
            };

            // Creamos la preferencia
            var result = await client.CreateAsync(preference);

            // ✅ EL ARREGLO CRÍTICO:
            // Aunque tu token sea APP_USR-, DEBES retornar SandboxInitPoint para que 
            // la pasarela acepte las tarjetas de prueba (4509...) y no de error de "Something went wrong".
            return result.SandboxInitPoint; 
        }
    }
}