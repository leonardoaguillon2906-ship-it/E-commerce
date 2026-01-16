using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "Correo electrónico no válido")]
        public string Email { get; set; } = string.Empty;
    }
}
