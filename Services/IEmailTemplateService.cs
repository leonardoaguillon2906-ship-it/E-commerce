using System.Threading.Tasks;
using EcommerceApp.Models;

namespace EcommerceApp.Services
{
    public interface IEmailTemplateService
    {
        Task<string> LoadAsync(string templateName);
        Task<string> BuildOrderConfirmationEmail(Order order);
    }
}
