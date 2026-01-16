using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CART_KEY = "CART";

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // VER CARRITO
        // URL: /Cliente/Cart
        // =========================
        public IActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }

        // =========================
        // AGREGAR PRODUCTO
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            var product = await _context.Products.FindAsync(productId);

            // Validar existencia y stock
            if (product == null || product.Stock <= 0)
            {
                TempData["Error"] = "Producto sin stock disponible";
                return RedirectToAction("Index", "Products", new { area = "Cliente" });
            }

            var cart = GetCart();

            var item = cart.FirstOrDefault(c => c.ProductId == productId);

            if (item != null)
            {
                // No permitir exceder stock
                if (item.Quantity + quantity > product.Stock)
                {
                    TempData["Error"] = $"No hay suficiente stock. Stock disponible: {product.Stock}";
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });
                }

                item.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Quantity = quantity,
                    ImageUrl = product.ImageUrl
                });
            }

            SaveCart(cart);

            return RedirectToAction("Index", "Cart", new { area = "Cliente" });
        }

        // =========================
        // ELIMINAR PRODUCTO
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);

            if (item != null)
                cart.Remove(item);

            SaveCart(cart);

            return RedirectToAction("Index", "Cart", new { area = "Cliente" });
        }

        // =========================
        // LIMPIAR CARRITO
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(CART_KEY);
            return RedirectToAction("Index", "Cart", new { area = "Cliente" });
        }

        // =========================
        // FINALIZAR COMPRA (STOCK)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();

            if (cart.Count == 0)
            {
                TempData["Error"] = "El carrito está vacío.";
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });
            }

            foreach (var item in cart)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.Stock < item.Quantity)
                {
                    TempData["Error"] = $"No hay stock suficiente para el producto {item.Name}.";
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });
                }

                // Restar stock
                product.Stock -= item.Quantity;
                _context.Update(product);
            }

            await _context.SaveChangesAsync();

            // Limpiar carrito
            Clear();

            TempData["Success"] = "Compra realizada con éxito.";
            return RedirectToAction("Index", "Products", new { area = "Cliente" });
        }

        // =========================
        // MÉTODOS PRIVADOS
        // =========================
        private List<CartItem> GetCart()
        {
            return HttpContext.Session.GetObject<List<CartItem>>(CART_KEY)
                   ?? new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetObject(CART_KEY, cart);
        }
    }
}
