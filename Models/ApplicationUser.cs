using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace EcommerceApp.Models
{
    public class ApplicationUser : IdentityUser
    {
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
