using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MiniMarket.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class SeguridadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SeguridadController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadBackup()
        {
            string backupFolder = Path.Combine(_env.WebRootPath, "backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string fileName = $"Backup_MiniMarket_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            string filePath = Path.Combine(backupFolder, fileName);

            try
            {
                var connection = _context.Database.GetDbConnection() as SqlConnection;
                var dbName = connection.Database;
                string sql = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, NAME = 'MiniMarket Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";
                
                await _context.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@path", filePath));

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                
                // Cleanup temp locally created
                System.IO.File.Delete(filePath);

                return File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar la copia de seguridad: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(IFormFile backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["Error"] = "Seleccione un archivo válido para restaurar (.bak).";
                return RedirectToAction(nameof(Index));
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "temp_restore");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            
            string filePath = Path.Combine(uploadsFolder, Guid.NewGuid() + ".bak");

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await backupFile.CopyToAsync(stream);
                }

                var connectionString = _context.Database.GetConnectionString();
                var masterConnectionString = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

                var dbName = _context.Database.GetDbConnection().Database;

                string sqlSingleUser = $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                string sqlRestore = $"RESTORE DATABASE [{dbName}] FROM DISK = '{filePath}' WITH REPLACE;";
                string sqlMultiUser = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";

                using (var masterConn = new SqlConnection(masterConnectionString))
                {
                    await masterConn.OpenAsync();

                    using (var cmd = new SqlCommand(sqlSingleUser, masterConn)) { await cmd.ExecuteNonQueryAsync(); }
                    using (var cmd = new SqlCommand(sqlRestore, masterConn)) { await cmd.ExecuteNonQueryAsync(); }
                    using (var cmd = new SqlCommand(sqlMultiUser, masterConn)) { await cmd.ExecuteNonQueryAsync(); }
                }

                System.IO.File.Delete(filePath);

                TempData["Mensaje"] = "Base de datos restaurada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Attempt to put back to multi-user if failed midway
                try
                {
                   var masterConnectionString = new SqlConnectionStringBuilder(_context.Database.GetConnectionString()) { InitialCatalog = "master" }.ConnectionString;
                   var dbName = _context.Database.GetDbConnection().Database;
                   using (var masterConn = new SqlConnection(masterConnectionString))
                   {
                       masterConn.Open();
                       using (var cmd = new SqlCommand($"ALTER DATABASE [{dbName}] SET MULTI_USER;", masterConn)) { cmd.ExecuteNonQuery(); }
                   }
                } catch { }

                TempData["Error"] = "Error crítico en restauración: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FactoryReset()
        {
            try
            {
                string sql = @"
                    EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
                    DELETE FROM DetalleVentas;
                    DELETE FROM Ventas;
                    DELETE FROM DetalleCompras;
                    DELETE FROM Compras;
                    DELETE FROM MovimientosInventario;
                    DELETE FROM AperturasCaja;
                    DELETE FROM Productos;
                    DELETE FROM Categorias;
                    DELETE FROM Clientes;
                    DELETE FROM Proveedores;
                    
                    DBCC CHECKIDENT ('Ventas', RESEED, 0);
                    DBCC CHECKIDENT ('Compras', RESEED, 0);
                    DBCC CHECKIDENT ('DetalleVentas', RESEED, 0);
                    DBCC CHECKIDENT ('DetalleCompras', RESEED, 0);
                    DBCC CHECKIDENT ('MovimientosInventario', RESEED, 0);
                    DBCC CHECKIDENT ('AperturasCaja', RESEED, 0);
                    DBCC CHECKIDENT ('Productos', RESEED, 0);
                    DBCC CHECKIDENT ('Categorias', RESEED, 0);
                    DBCC CHECKIDENT ('Clientes', RESEED, 0);
                    DBCC CHECKIDENT ('Proveedores', RESEED, 0);
                    
                    EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
                ";

                await _context.Database.ExecuteSqlRawAsync(sql);

                TempData["Mensaje"] = "¡Reseteo de fábrica exitoso! Se ha eliminado todo el historial pero se conservó la configuración visual y tu usuario Administrador.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al resetear base de datos: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
