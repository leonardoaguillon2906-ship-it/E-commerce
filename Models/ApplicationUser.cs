using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace EcommerceApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        // ============================
        // DATOS PERSONALIZADOS
        // ============================
        // ✅ AGREGADO: Propiedad para almacenar el nombre completo del usuario
        public string? FullName { get; set; }

        // ============================
        // RELACIÓN: CARRITO DE COMPRAS
        // ============================
        public virtual ICollection<CartItem> CartItems { get; set; }

        // ============================
        // RELACIÓN: ÓRDENES
        // ============================
        public virtual ICollection<Order> Orders { get; set; }

        // ============================
        // CONSTRUCTOR
        // ============================
        public ApplicationUser()
        {
            CartItems = new HashSet<CartItem>();
            Orders = new HashSet<Order>();
        }
    }
}