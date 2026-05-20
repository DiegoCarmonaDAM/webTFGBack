using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================================
        // ENDPOINT EXISTENTE — usado por la WEB (Vue) para login de TRABAJADORES
        // NO TOCAR. Sigue rechazando a quienes no son trabajadores.
        // =====================================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.email) || string.IsNullOrWhiteSpace(request.pass))
                return BadRequest(new { message = "Email y contraseña son obligatorios" });

            var emailNorm = request.email.Trim().ToLower();

            // 1 — Buscar Persona por email
            var persona = await _context.Persona
                .FirstOrDefaultAsync(p => p.email != null &&
                                          p.email.ToLower().Trim() == emailNorm);

            if (persona == null)
                return Unauthorized(new { message = "Email o contraseña incorrectos" });

            // 2 — Verificar contraseña
            if (persona.pass.Trim() != request.pass.Trim())
                return Unauthorized(new { message = "Email o contraseña incorrectos" });

            // 3 — Verificar que es Trabajador
            var trabajador = await _context.Trabajador
                .FirstOrDefaultAsync(t => t.id_persona == persona.id_persona);

            if (trabajador == null)
                return Unauthorized(new { message = "Acceso denegado: solo trabajadores pueden entrar" });

            return Ok(new
            {
                message = "Login correcto",
                id_trabajador = trabajador.id_trabajador,
                rol = trabajador.rol,
                nombre = persona.nombre,
                email = persona.email,
                id_gym = trabajador.id_gym
            });
        }

        // =====================================================================
        // === NUEVO PARA APP MÓVIL =============================================
        // POST /api/auth/login-app
        // Login para la app Android. Acepta a CUALQUIER persona (cliente o
        // trabajador) que tenga un registro de Cliente asociado a su Persona.
        // Devuelve perfil completo + suscripción activa en una sola llamada
        // para que la app no tenga que hacer un segundo round-trip.
        // =====================================================================
        [HttpPost("login-app")]
        public async Task<IActionResult> LoginApp([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.email) || string.IsNullOrWhiteSpace(request.pass))
                return BadRequest(new { message = "Email y contraseña son obligatorios" });

            var emailNorm = request.email.Trim().ToLower();

            // 1 — Buscar Persona por email
            var persona = await _context.Persona
                .FirstOrDefaultAsync(p => p.email != null &&
                                          p.email.ToLower().Trim() == emailNorm);

            if (persona == null)
                return Unauthorized(new { message = "Email o contraseña incorrectos" });

            // 2 — Verificar contraseña
            if (persona.pass.Trim() != request.pass.Trim())
                return Unauthorized(new { message = "Email o contraseña incorrectos" });

            // 3 — Verificar que tiene un Cliente asociado (la app necesita un id_cliente)
            //     Trabajadores que TAMBIÉN sean clientes podrán entrar; trabajadores
            //     puros (sin Cliente) no, porque la app no tiene nada que mostrarles.
            var cliente = await _context.Cliente
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(c => c.id_persona == persona.id_persona);

            if (cliente == null)
                return Unauthorized(new
                {
                    message = "No estás registrado como cliente del gimnasio. Pide a tu gimnasio que te dé de alta."
                });

            // 4 — Calcular suscripción activa (la más reciente con estado=activa y fecha_fin>=hoy)
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var sus = cliente.Suscripciones
                .Where(s => s.estado == "activa" && s.fecha_fin >= hoy)
                .OrderByDescending(s => s.fecha_fin)
                .FirstOrDefault();

            string planActivo        = sus?.Plan?.nombre ?? "";
            int    accesosRestantes  = sus?.accesos_restantes ?? 0;
            string fechaFin          = sus != null ? sus.fecha_fin.ToString("dd/MM/yyyy") : "";
            int    diasRestantes     = sus != null && sus.fecha_fin >= hoy
                                       ? sus.fecha_fin.DayNumber - hoy.DayNumber
                                       : 0;
            string estadoSus         = sus?.estado ?? "sin_suscripcion";

            return Ok(new
            {
                message            = "Login correcto",
                id_cliente         = cliente.id_cliente,
                id_persona         = persona.id_persona,
                nombre             = persona.nombre,
                email              = persona.email,
                telefono           = persona.telefono,
                documento          = persona.documento_identidad,
                plan_activo        = planActivo,
                accesos_restantes  = accesosRestantes,
                fecha_fin          = fechaFin,
                dias_restantes     = diasRestantes,
                estado_suscripcion = estadoSus
            });
        }
        // === FIN AÑADIDO PARA APP MÓVIL ======================================
    }

    public class LoginRequest
    {
        public string email { get; set; } = string.Empty;
        public string pass { get; set; } = string.Empty;
    }
}
