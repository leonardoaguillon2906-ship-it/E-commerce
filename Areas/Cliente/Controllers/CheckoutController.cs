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
using EcommerceApp.Services; // ✅ Asegúrate de tener este using

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MercadoPagoService _mercadoPagoService;
        private readonly IEmailService _emailService; // ✅ Campo agregado

        // ✅ Constructor modificado para incluir el servicio de email
        public CheckoutController(
            ApplicationDbContext context, 
            MercadoPagoService mercadoPagoService, 
            IEmailService emailService)
        {
            _context = context;
            _mercadoPagoService = mercadoPagoService;
            _emailService = emailService;
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

            var initPoint = await _mercadoPagoService.CrearPago(order.Total, order.Id);

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
                // ✅ CARGA CRÍTICA: Se agregó .Include(o => o.User) para obtener el email del comprador
                order = await _context.Orders
                    .Include(o => o.User) 
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                // Limpiar carrito
                HttpContext.Session.Remove("CART");

                // Solo marcar como Procesando si está pendiente
                if (order != null && order.Status == "Pendiente")
                {
                    order.Status = "Procesando"; 
                    await _context.SaveChangesAsync();

                    // ✅ ENVÍO DE EMAIL: Se ejecuta aquí para asegurar que el pago fue exitoso
                    try 
                    {
                        await _emailService.SendOrderConfirmationEmail(order);
                    }
                    catch (Exception ex)
                    {
                        // Registramos el error en consola para no romper la experiencia del usuario
                        Console.WriteLine($"Error enviando email: {ex.Message}");
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