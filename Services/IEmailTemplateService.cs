using EcommerceApp.Models;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    /// <summary>
    /// Define los métodos necesarios para la gestión de plantillas de correo electrónico.
    /// </summary>
    public interface IEmailTemplateService
    {
        /// <summary>
        /// Carga el contenido de un archivo HTML desde la carpeta de plantillas.
        /// </summary>
        /// <param name="templateName">Nombre del archivo (ej. "OrderPaid.html").</param>
        /// <returns>El contenido del archivo como una cadena de texto.</returns>
        Task<string> LoadAsync(string templateName);

        /// <summary>
        /// Toma una orden, carga la plantilla de confirmación y reemplaza los marcadores 
        /// de posición con los datos reales del pedido y del cliente.
        /// </summary>
        /// <param name="order">La entidad de la orden con sus items y usuario incluidos.</param>
        /// <returns>El HTML final listo para ser enviado por correo.</returns>
        Task<string> BuildOrderConfirmationEmail(Order order);
    }
}