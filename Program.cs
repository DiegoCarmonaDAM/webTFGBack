using webTFGBack.data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// CORS para Vue
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173",
                "https://adrtfg.netlify.app")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Conexión a MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "La cadena de conexión 'DefaultConnection' no está configurada. Comprueba appsettings.json o las variables de entorno."
    );
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseCors("AllowVue");

//app.UseHttpsRedirection();    // PRODUCCIÓN — activo

app.UseAuthorization();

app.MapControllers();

// --- PRUEBA DE CONEXIÓN A LA BASE DE DATOS ---
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

try
{
    var canConnect = db.Database.CanConnect();
    Console.WriteLine($"Conexión a la base de datos: {canConnect}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error conectando a la base de datos: {ex.Message}");
}
// ----------------------------------------------

app.Run();