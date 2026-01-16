using System;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class HomeBanner
    {
        // =======================
        // CLAVE PRIMARIA
        // =======================
        [Key]
        public int Id { get; set; }

        // =======================
        // TEXTO DEL BANNER
        // =======================
        [StringLength(150)]
        public string? Title { get; set; }

        [StringLength(300)]
        public string? Subtitle { get; set; }

        // =======================
        // MEDIA (IMAGEN / VIDEO)
        // =======================
        [Required]
        public string MediaUrl { get; set; } = string.Empty;

        // true = video | false = imagen
        public bool IsVideo { get; set; }

        // =======================
        // CONTROL
        // =======================
        public bool IsActive { get; set; } = true;

        public int Order { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
