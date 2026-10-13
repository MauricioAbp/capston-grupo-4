using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Base de Datos
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Identidad (Usuarios y Roles)
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 3. Servicios Web
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // ¡IMPORTANTE PARA EL LOGIN!
builder.Services.AddHttpClient<MiniMarket.Web.Services.FactilizaService>();
builder.Services.AddHttpClient<MiniMarket.Web.Services.IAAsistenteService>();

var app = builder.Build();

// 4. Inicializar Datos (Sembrador)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SembradorDatos.Inicializar(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al sembrar datos.");
    }
}

// 5. Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages(); // ¡IMPORTANTE!

app.Run();