using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace EcommerceApp.Models
{
    public class Product
    {
        // =======================
        // CLAVE PRIMARIA
        // =======================
        [Key]
        public int Id { get; set; }

        // =======================
        // PROPIEDADES PRINCIPALES
        // =======================
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre del producto")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 999999)]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Precio")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(500)]
        [Display(Name = "Descripción")]
        public string Description { get; set; } = string.Empty;

        // Imagen principal del producto
        [StringLength(500)]
        [Display(Name = "Imagen principal")]
        public string? ImageUrl { get; set; }

        // =======================
        // MEDIA PARA BANNER HOME
        // =======================

        // Ruta del archivo multimedia (imagen o video)
        [StringLength(500)]
        [Display(Name = "Media del banner")]
        public string? BannerMediaUrl { get; set; }

        // true = video | false = imagen
        [Display(Name = "¿Es video?")]
        public bool IsBannerVideo { get; set; } = false;

        // =======================
        // STOCK
        // =======================
        [Required]
        [Range(0, 99999)]
        [Display(Name = "Stock disponible")]
        public int Stock { get; set; }

        // =======================
        // CATEGORÍA
        // =======================
        [Display(Name = "Categoría")]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        // =======================
        // RELACIONES
        // =======================

        // Evita ciclos en serialización JSON (si usas APIs)
        [NotMapped]
        public ICollection<CartItem>? CartItems { get; set; }

        public ICollection<OrderItem>? OrderItems { get; set; }

        // =======================
        // CONTROL DEL SISTEMA
        // =======================
        [Display(Name = "Fecha de creación")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Producto usado como banner del Home
        [Display(Name = "Mostrar en banner")]
        public bool IsHomeBanner { get; set; } = false;

        // =======================
        // VISIBILIDAD DEL PRODUCTO
        // =======================
        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
