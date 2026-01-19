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

        // ===============================
        // Mensaje dinámico para la vista según el estado del pago
        // ===============================
        [NotMapped]
        public string DisplayMessage
        {
            get
            {
                return Status switch
                {
                    "Procesando" => "¡Pago aprobado! Tu pedido está en proceso.",
                    "Pendiente" => "¡Ya casi es tuyo! Estamos revisando tu pago. Dentro de las próximas 24 horas te avisaremos por e-mail si se acreditó.",
                    "Rechazado" => "Tu pago fue rechazado. Por favor intenta nuevamente.",
                    _ => "Estado del pago desconocido. Contacta soporte si el problema persiste."
                };
            }
        }

        // ===============================
        // Color dinámico para la vista según el estado
        // ===============================
        [NotMapped]
        public string DisplayColor
        {
            get
            {
                return Status switch
                {
                    "Procesando" => "text-success",
                    "Pendiente" => "text-warning",
                    "Rechazado" => "text-danger",
                    _ => "text-muted"
                };
            }
        }
    }
}
