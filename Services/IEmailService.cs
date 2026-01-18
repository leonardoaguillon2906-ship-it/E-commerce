using EcommerceApp.Models;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    public interface IEmailService
    {
        // Este es el método que invoca tu CheckoutController
        Task SendOrderConfirmationEmail(Order order);
    }
}