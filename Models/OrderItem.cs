using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        // ===============================
        // RELACIÓN CON ORDER (OBLIGATORIA)
        // ===============================
        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        // ===============================
        // RELACIÓN CON PRODUCT
        // ===============================
        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;

        // ===============================
        // CANTIDAD
        // ===============================
        [Required]
        [Range(1, 9999)]
        public int Quantity { get; set; }

        // ===============================
        // PRECIO UNITARIO (NO TOTAL)
        // ===============================
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
    }
}
