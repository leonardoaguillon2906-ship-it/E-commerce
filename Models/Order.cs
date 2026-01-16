using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        // ===============================
        // RELACIÓN CON USUARIO (OBLIGATORIA)
        // ===============================
        [Required]
        public string UserId { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        // ===============================
        // FECHA DE CREACIÓN
        // ===============================
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===============================
        // TOTAL DE LA ORDEN
        // ===============================
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // ===============================
        // ESTADO DE LA ORDEN
        // ===============================
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pendiente";

        // ===============================
        // ITEMS DE LA ORDEN (RELACIÓN REAL)
        // ===============================
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
