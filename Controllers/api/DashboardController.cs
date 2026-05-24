using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DashboardController(AppDbContext context) => _context = context;

        // Valida que el gym pertenece a la compañía recibida
        private async Task<bool> GymPerteneceACompania(int idGym, int idCompania) =>
            await _context.Gym.AnyAsync(g => g.id_gym == idGym && g.id_compania == idCompania);

        // GET api/dashboard/stats?idGym=1&idCompania=1
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] int idGym, [FromQuery] int idCompania)
        {
            if (!await GymPerteneceACompania(idGym, idCompania))
                return Forbid();

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);

            var totalClientes = await _context.Suscripcion
                .Where(s => s.Plan!.id_compania == idCompania && s.estado == "activa")
                .Select(s => s.id_cliente)
                .Distinct()
                .CountAsync();

            var ingresosMes = await _context.Suscripcion
                .Where(s => s.Plan!.id_compania == idCompania
                         && s.fecha_inicio >= inicioMes
                         && s.fecha_inicio <= hoy)
                .SumAsync(s => (decimal?)s.Plan!.precio) ?? 0;

            var vencenHoy = await _context.Suscripcion
                .Where(s => s.Plan!.id_compania == idCompania
                         && s.fecha_fin == hoy
                         && s.estado == "activa")
                .CountAsync();

            var altasMes = await _context.Suscripcion
                .Where(s => s.Plan!.id_compania == idCompania
                         && s.fecha_inicio >= inicioMes
                         && s.fecha_inicio <= hoy)
                .CountAsync();

            return Ok(new { totalClientes, ingresosMes, vencenHoy, altasMes });
        }

        // GET api/dashboard/miembros-recientes?idGym=1&idCompania=1
        [HttpGet("miembros-recientes")]
        public async Task<IActionResult> GetMiembrosRecientes([FromQuery] int idGym, [FromQuery] int idCompania)
        {
            if (!await GymPerteneceACompania(idGym, idCompania))
                return Forbid();

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var en7Dias = hoy.AddDays(7);

            var miembros = await _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones)
                    .ThenInclude(s => s.Plan)
                .Where(c => c.Suscripciones.Any(s => s.Plan!.id_compania == idCompania)
                || !c.Suscripciones.Any())
                .OrderByDescending(c => c.id_cliente)
                .Take(8)
                .Select(c => new
                {
                    id = c.id_cliente,
                    nombre = c.Persona!.nombre,
                    plan = c.Suscripciones
                               .Where(s => s.estado == "activa" && s.Plan!.id_compania == idCompania)
                               .OrderByDescending(s => s.fecha_inicio)
                               .Select(s => s.Plan!.nombre)
                               .FirstOrDefault() ?? "Sin plan",
                    status = c.Suscripciones
                               .Any(s => s.estado == "activa"
                                      && s.Plan!.id_compania == idCompania
                                      && s.fecha_fin <= en7Dias)
                               ? "warning" : "active"
                })
                .ToListAsync();

            return Ok(miembros);
        }

        // GET api/dashboard/ocupacion?idGym=1&idCompania=1
        [HttpGet("ocupacion")]
        public async Task<IActionResult> GetOcupacion([FromQuery] int idGym, [FromQuery] int idCompania)
        {
            if (!await GymPerteneceACompania(idGym, idCompania))
                return Forbid();

            var hoyDt = DateTime.Today;
            var manyanaDt = hoyDt.AddDays(1);

            var dentro = await _context.RegistroEntrada
                .CountAsync(r => r.id_gym == idGym
                              && r.fecha_hora_entrada >= hoyDt
                              && r.fecha_hora_entrada < manyanaDt
                              && r.fecha_hora_salida == null);

            var totalHoy = await _context.RegistroEntrada
                .CountAsync(r => r.id_gym == idGym
                              && r.fecha_hora_entrada >= hoyDt
                              && r.fecha_hora_entrada < manyanaDt);

            int aforoMax = 100;

            return Ok(new
            {
                dentro,
                totalHoy,
                aforoMax,
                porcentaje = aforoMax > 0 ? Math.Round((double)dentro / aforoMax * 100, 1) : 0
            });
        }
    }
}