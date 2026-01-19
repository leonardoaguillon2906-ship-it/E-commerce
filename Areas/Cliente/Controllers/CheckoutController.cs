using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services.Payments;
using EcommerceApp.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MercadoPagoService _mercadoPagoService;

        public CheckoutController(ApplicationDbContext context, MercadoPagoService mercadoPagoService)
        {
            _context = context;
            _mercadoPagoService = mercadoPagoService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });

            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm()
        {
            var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // ✅ Capturamos el email del usuario autenticado para el Payer de Mercado Pago
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            // Validar stock antes de crear la orden
            foreach (var item in cart)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.Stock < item.Quantity)
                {
                    TempData["Error"] = $"No hay stock suficiente para {item.Name}";
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });
                }
            }

            var total = cart.Sum(i => i.Price * i.Quantity);

            var order = new Order
            {
                UserId = userId,
                Total = total,
                Status = "Pendiente",
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in cart)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }

            await _context.SaveChangesAsync();

            // ✅ Ajuste: Enviamos el email del comprador al servicio de Mercado Pago
            // Si el email es nulo (por configuración de Claims), enviamos el de prueba que creaste
            var compradorEmail = userEmail ?? "TESTUSER2412529260038779833";
            
            var initPoint = await _mercadoPagoService.CrearPago(order.Total, order.Id, compradorEmail);

            if (string.IsNullOrEmpty(initPoint))
            {
                TempData["Error"] = "Error al conectar con la pasarela de pago.";
                return RedirectToAction("Index", "Cart");
            }

            return Redirect(initPoint);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string payment_id, string status, string external_reference)
        {
            Order? order = null;

            if (!string.IsNullOrEmpty(external_reference) &&
                int.TryParse(external_reference, out int orderId))
            {
                order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order != null)
                {
                    // Limpiar carrito solo si la orden fue encontrada
                    HttpContext.Session.Remove("CART");

                    if (order.Status == "Pendiente")
                    {
                        order.Status = "Procesando"; 
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return View(order); 
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Pending(string external_reference)
        {
            if (!string.IsNullOrEmpty(external_reference) &&
                int.TryParse(external_reference, out int orderId))
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.Status = "Pendiente";
                    await _context.SaveChangesAsync();
                }
            }

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Failure(string external_reference)
        {
            if (!string.IsNullOrEmpty(external_reference) &&
                int.TryParse(external_reference, out int orderId))
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.Status = "Rechazado";
                    await _context.SaveChangesAsync();
                }
            }

            return View();
        }
    }
}