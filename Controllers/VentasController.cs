using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;
using MiniMarket.Web.Models;
using MiniMarket.Web.Services; // <--- Importante para reconocer FactilizaService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniMarket.Web.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FactilizaService _factilizaService; // <--- Declarado correctamente aquí

        public VentasController(ApplicationDbContext context, FactilizaService factilizaService)
        {
            _context = context;
            _factilizaService = factilizaService; // <--- Inicializado en el constructor
        }

        // GET: Ventas (Historial)
        public async Task<IActionResult> Index()
        {
            var ventas = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.Cliente)
                .OrderByDescending(v => v.Fecha)
                .ToListAsync();
            return View(ventas);
        }

        // GET: Punto de Venta
        public IActionResult Create()
        {
            var cajaAbierta = _context.AperturasCaja
                .Any(c => c.UsuarioId == User.Identity.Name && c.Estado == true);

            if (!cajaAbierta)
            {
                TempData["MensajeError"] = "⚠️ ¡Atención! Debes ABRIR CAJA antes de poder vender.";
                return RedirectToAction("Index", "Caja");
            }
            return View();
        }

        // API: Buscar Productos
        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new { results = new object[] { } });

            var productos = await _context.Productos
                .Where(p => p.Estado == true && (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
                .Select(p => new
                {
                    id = p.Id,
                    text = p.Nombre + " | Stock: " + p.Stock,
                    nombre = p.Nombre,
                    codigo = p.CodigoBarras,
                    precio = p.Precio,
                    stock = p.Stock
                })
                .Take(10)
                .ToListAsync();

            return Json(new { results = productos });
        }

        // API: Buscar Clientes
        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new { results = new object[] { } });

            var clientes = await _context.Clientes
                .Where(c => c.Nombre.Contains(term) || c.Documento.Contains(term))
                .Select(c => new { id = c.Id, text = c.Nombre })
                .Take(10)
                .ToListAsync();

            return Json(new { results = clientes });
        }

        // POST: Registrar Venta
        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] VentaRequest request)
        {
            var cajaAbierta = _context.AperturasCaja
                .Any(c => c.UsuarioId == User.Identity.Name && c.Estado == true);

            if (!cajaAbierta)
                return Json(new { exito = false, mensaje = "⛔ LA CAJA ESTÁ CERRADA. No se puede procesar la venta." });

            if (request == null || request.Detalles == null || request.Detalles.Count == 0)
                return Json(new { exito = false, mensaje = "Datos inválidos" });

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var nuevaVenta = new Venta
                    {
                        Fecha = DateTime.Now,
                        Total = request.Total,
                        Descuento = request.Descuento,
                        MetodoPago = string.IsNullOrEmpty(request.MetodoPago) ? "Efectivo" : request.MetodoPago,
                        Estado = "Activa",
                        UsuarioId = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name)?.Id,
                        ClienteId = request.ClienteId
                    };

                    _context.Ventas.Add(nuevaVenta);
                    await _context.SaveChangesAsync();

                    foreach (var item in request.Detalles)
                    {
                        var producto = await _context.Productos.FindAsync(item.ProductoId);
                        if (producto == null) throw new Exception("Producto no encontrado");
                        if (producto.Stock < item.Cantidad)
                            throw new Exception($"Stock insuficiente para {producto.Nombre}");

                        producto.Stock -= item.Cantidad;

                        _context.DetalleVentas.Add(new DetalleVenta
                        {
                            VentaId = nuevaVenta.Id,
                            ProductoId = item.ProductoId,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.Precio,
                            SubTotal = item.Total
                        });

                        _context.MovimientosInventario.Add(new MovimientoInventario
                        {
                            Fecha = DateTime.Now,
                            ProductoId = item.ProductoId,
                            TipoMovimiento = "SALIDA",
                            Cantidad = item.Cantidad,
                            Usuario = User.Identity.Name,
                            Referencia = $"Venta #{nuevaVenta.Id}"
                        });
                    }

                    await _context.SaveChangesAsync();

                    // Emisión electrónica con Factiliza
                    var cliente = request.ClienteId.HasValue 
                        ? await _context.Clientes.FindAsync(request.ClienteId.Value) 
                        : null;

                    string numeroDoc = cliente?.Documento ?? "00000000";
                    string nombreCliente = cliente?.Nombre ?? "Clientes Varios";
                    decimal igvCalculado = request.Total * 0.18m;

                    await _factilizaService.EmitirBoletaAsync(numeroDoc, nombreCliente, request.Total, igvCalculado);

                    await transaction.CommitAsync();

                    return Json(new { exito = true, mensaje = "Venta registrada correctamente", idVenta = nuevaVenta.Id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { exito = false, mensaje = ex.Message });
                }
            }
        }

        // GET: Ticket de Venta
        public async Task<IActionResult> Ticket(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (venta == null) return NotFound();

            var config = await _context.Configuraciones.FirstOrDefaultAsync();
            ViewBag.Configuracion = config ?? new Configuracion
            {
                NombreEmpresa = "AmayaSoft",
                Ruc = "00000000000",
                Direccion = "Configure el sistema",
                IgvPorcentaje = 18
            };

            return View(venta);
        }

        // POST: Anular Venta (solo Admin)
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var venta = await _context.Ventas
                        .Include(v => v.Detalles)
                        .FirstOrDefaultAsync(v => v.Id == id);

                    if (venta == null)
                        return Json(new { exito = false, mensaje = "Venta no encontrada." });

                    if (venta.Estado == "Anulada")
                        return Json(new { exito = false, mensaje = "Esta venta ya está anulada." });

                    venta.Estado = "Anulada";

                    foreach (var detalle in venta.Detalles)
                    {
                        var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                        if (producto != null)
                            producto.Stock += detalle.Cantidad;

                        _context.MovimientosInventario.Add(new MovimientoInventario
                        {
                            Fecha = DateTime.Now,
                            ProductoId = detalle.ProductoId,
                            TipoMovimiento = "AJUSTE-ENTRADA",
                            Cantidad = detalle.Cantidad,
                            Usuario = User.Identity.Name,
                            Referencia = $"Anulación Venta #{venta.Id}",
                            Motivo = "Venta anulada por administrador"
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { exito = true, mensaje = $"Venta #{id} anulada. Stock revertido correctamente." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { exito = false, mensaje = ex.Message });
                }
            }
        }
    }

    public class VentaRequest
    {
        public int? ClienteId { get; set; }
        public decimal Total { get; set; }
        public decimal Descuento { get; set; }
        public string MetodoPago { get; set; }
        public List<DetalleRequest> Detalles { get; set; }
    }

    public class DetalleRequest
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; }
        public decimal Precio { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }
}
