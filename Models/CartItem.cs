using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        // Relación con el usuario que posee este item del carrito
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        // Relación con el producto agregado
        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        // Nombre del producto (para mostrar en el carrito)
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Precio unitario del producto
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Cantidad agregada al carrito
        [Required]
        [Range(1, 9999, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public int Quantity { get; set; }

        // URL de la imagen del producto
        public string? ImageUrl { get; set; }

        // Propiedad calculada que no se guarda en la base de datos
        [NotMapped]
        public decimal Total => Price * Quantity;
    }
}
