using EcommerceApp.Models;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    public interface IEmailService
    {
        // Este método es el que busca el CheckoutController
        Task SendOrderConfirmationEmail(Order order);
    }
}