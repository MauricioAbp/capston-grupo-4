using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniMarket.Web.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsuariosController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // --- LISTAR ---
        public async Task<IActionResult> Index()
        {
            var usuarios = await _userManager.Users.ToListAsync();
            var lista = new List<UsuarioViewModel>();

            foreach (var user in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(user);
                lista.Add(new UsuarioViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    Rol = roles.FirstOrDefault() ?? "Sin Rol"
                });
            }

            return View(lista);
        }

        // --- CREAR (VISTA) ---
        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(_roleManager.Roles, "Name", "Name");
            return View();
        }

        // --- CREAR (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioRegistroModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(model.Rol))
                        await _userManager.AddToRoleAsync(user, model.Rol);

                    TempData["Exito"] = "Usuario creado correctamente.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            ViewBag.Roles = new SelectList(_roleManager.Roles, "Name", "Name");
            return View(model);
        }

        // --- EDITAR (VISTA) ---
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UsuarioEditModel
            {
                Id = user.Id,
                Email = user.Email,
                RolActual = roles.FirstOrDefault() ?? ""
            };

            ViewBag.Roles = new SelectList(_roleManager.Roles, "Name", "Name", model.RolActual);
            return View(model);
        }

        // --- EDITAR (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UsuarioEditModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // Cambiar rol
            var rolesActuales = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, rolesActuales);
            if (!string.IsNullOrEmpty(model.NuevoRol))
                await _userManager.AddToRoleAsync(user, model.NuevoRol);

            // Cambiar contraseña solo si se proporcionó una nueva
            if (!string.IsNullOrEmpty(model.NuevaPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resultPass = await _userManager.ResetPasswordAsync(user, token, model.NuevaPassword);
                if (!resultPass.Succeeded)
                {
                    foreach (var error in resultPass.Errors)
                        ModelState.AddModelError("", error.Description);

                    ViewBag.Roles = new SelectList(_roleManager.Roles, "Name", "Name", model.NuevoRol);
                    return View(model);
                }
            }

            TempData["Exito"] = "Usuario actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // --- ELIMINAR (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var userActual = await _userManager.GetUserAsync(User);
            if (userActual?.Id == id)
                return Json(new { exito = false, mensaje = "No puedes eliminar tu propia cuenta." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return Json(new { exito = false, mensaje = "Usuario no encontrado." });

            await _userManager.DeleteAsync(user);
            return Json(new { exito = true, mensaje = "Usuario eliminado correctamente." });
        }
    }
}

namespace MiniMarket.Web.Models
{
    public class UsuarioViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
    }

    public class UsuarioRegistroModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Rol { get; set; }
    }

    public class UsuarioEditModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string RolActual { get; set; }
        public string NuevoRol { get; set; }
        public string NuevaPassword { get; set; }
    }
}