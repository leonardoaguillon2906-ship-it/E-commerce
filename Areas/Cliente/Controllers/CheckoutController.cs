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
using Microsoft.Extensions.Logging;

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MercadoPagoService _mercadoPagoService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ApplicationDbContext context, MercadoPagoService mercadoPagoService, ILogger<CheckoutController> logger)
        {
            _context = context;
            _mercadoPagoService = mercadoPagoService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

                if (cart == null || !cart.Any())
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });

                return View(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el carrito para Checkout");
                TempData["Error"] = "Ocurrió un error al cargar el carrito.";
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm()
        {
            try
            {
                var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

                if (cart == null || !cart.Any())
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Account");

                // Validar stock
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al confirmar el checkout");
                TempData["Error"] = "Ocurrió un error procesando su orden. Intente nuevamente.";
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string payment_id, string status, string external_reference)
        {
            string message = string.Empty;
            Order? order = null;

            try
            {
                if (!string.IsNullOrEmpty(external_reference) &&
                    int.TryParse(external_reference, out int orderId))
                {
                    order = await _context.Orders
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order != null)
                    {
                        HttpContext.Session.Remove("CART");

                        switch (status?.ToLower())
                        {
                            case "approved":
                                if (order.Status != "Procesando")
                                {
                                    order.Status = "Procesando";
                                    await _context.SaveChangesAsync();
                                }
                                message = "¡Pago aprobado! Tu pedido está en proceso.";
                                break;

                            case "pending":
                            case "in_process":
                            case "review_manual":
                                order.Status = "Pendiente";
                                await _context.SaveChangesAsync();
                                message = "Tu pago está pendiente. Te notificaremos por correo cuando se acredite.";
                                break;

                            case "rejected":
                                order.Status = "Rechazado";
                                await _context.SaveChangesAsync();
                                message = "Tu pago fue rechazado. Por favor intenta nuevamente.";
                                break;

                            default:
                                message = "Estado del pago desconocido. Contacta soporte si el problema persiste.";
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando Success");
                message = "Ocurrió un error procesando tu orden.";
            }

            ViewBag.Message = message;
            return View(order);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Pending(string external_reference)
        {
            string message = "Tu pago está pendiente. Te notificaremos por correo cuando se acredite.";
            try
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando Pending");
                message = "Ocurrió un error procesando tu orden.";
            }

            ViewBag.Message = message;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Failure(string external_reference)
        {
            string message = "Tu pago fue rechazado. Por favor intenta nuevamente.";
            try
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando Failure");
                message = "Ocurrió un error procesando tu orden.";
            }

            ViewBag.Message = message;
            return View();
        }
    }
}
