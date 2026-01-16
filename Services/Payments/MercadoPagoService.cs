using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcommerceApp.Services.Payments
{
    public class MercadoPagoService
    {
        private readonly IConfiguration _configuration;

        public MercadoPagoService(IConfiguration configuration)
        {
            _configuration = configuration;

            MercadoPagoConfig.AccessToken =
                _configuration["PaymentProviders:MercadoPago:AccessToken"];
        }

        public async Task<string> CrearPago(decimal total, int orderId)
        {
            var client = new PreferenceClient();

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

                // 🔥 WEBHOOK DIRECTO (CRÍTICO)
                NotificationUrl = _configuration["PaymentProviders:MercadoPago:WebhookUrl"],

                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = "https://spectrohelioscopic-porpoiselike-wilber.ngrok-free.dev/Cliente/Checkout/Success",
                    Failure = "https://spectrohelioscopic-porpoiselike-wilber.ngrok-free.dev/Cliente/Checkout/Failure",
                    Pending = "https://spectrohelioscopic-porpoiselike-wilber.ngrok-free.dev/Cliente/Checkout/Pending"
                },

                AutoReturn = "approved",

                // ❌ QUITAMOS BinaryMode (rompe tarjetas en sandbox)
                // BinaryMode = true,

                // ❌ NO forzar cuotas
                // PaymentMethods = new PreferencePaymentMethodsRequest
                // {
                //     Installments = 1,
                //     DefaultInstallments = 1
                // },

                ExternalReference = orderId.ToString()
            };

            var result = await client.CreateAsync(preference);

            return result.SandboxInitPoint;
        }
    }
}
