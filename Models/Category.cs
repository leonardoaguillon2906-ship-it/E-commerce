using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        // No es necesario [Required] en tipos no-nullables (bool)
        public bool IsActive { get; set; } = true;

        // Relación 1 → N con Product
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
