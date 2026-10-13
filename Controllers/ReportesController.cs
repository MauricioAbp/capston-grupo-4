using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;
using MiniMarket.Web.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniMarket.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Ventas(DateTime? fechaInicio, DateTime? fechaFin)
        {
            // Rango por defecto: últimos 30 días hasta hoy
            DateTime inicio = fechaInicio ?? DateTime.Today.AddDays(-30);
            DateTime fin = (fechaFin ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

            var ventas = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Usuario)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.Fecha >= inicio && v.Fecha <= fin && v.Estado == "Activa")
                .OrderByDescending(v => v.Fecha)
                .ToListAsync();

            var modelo = ventas.Select(v => new ReporteVentasViewModel
            {
                VentaId = v.Id,
                Fecha = v.Fecha,
                Cliente = v.Cliente != null ? v.Cliente.Nombre : "Público General",
                Cajero = v.Usuario != null ? v.Usuario.UserName : "Sistema",
                MetodoPago = v.MetodoPago,
                CantidadArticulos = v.Detalles.Sum(d => d.Cantidad),
                TotalVenta = v.Total,
                CostoTotal = v.Detalles.Sum(d => (d.Producto != null ? d.Producto.Costo : 0) * d.Cantidad)
            }).ToList();

            ViewBag.FechaInicio = inicio.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = (fechaFin ?? DateTime.Today).ToString("yyyy-MM-dd");

            return View(modelo);
        }
    }
}