using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SuscripcionController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SuscripcionController(AppDbContext context) => _context = context;

        // GET api/suscripcion/resumen?idCompania=1
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] int idCompania)
        {
            if (idCompania == 0)
                return BadRequest(new { message = "idCompania es obligatorio" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);

            var suscMes = await _context.Suscripcion
                .Include(s => s.Plan)
                .Where(s => s.fecha_inicio >= inicioMes
                         && s.Plan!.id_compania == idCompania)   // ← filtro
                .ToListAsync();

            var facturado = suscMes.Sum(s => s.Plan?.precio ?? 0);
            var cobrado = suscMes.Where(s => s.estado == "activa").Sum(s => s.Plan?.precio ?? 0);
            var pendiente = facturado - cobrado;

            var vencidas = await _context.Suscripcion
                .CountAsync(s => s.estado == "vencida"
                              && s.fecha_fin >= inicioMes
                              && s.Plan!.id_compania == idCompania);  // ← filtro

            return Ok(new { facturado, cobrado, pendiente, vencidas });
        }

        // GET api/suscripcion/ingresos-mensuales?idCompania=1
        [HttpGet("ingresos-mensuales")]
        public async Task<IActionResult> GetIngresosMensuales([FromQuery] int idCompania)
        {
            if (idCompania == 0)
                return BadRequest(new { message = "idCompania es obligatorio" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var desde = hoy.AddMonths(-5).AddDays(1 - hoy.Day);

            var datos = await _context.Suscripcion
                .Include(s => s.Plan)
                .Where(s => s.fecha_inicio >= desde
                         && s.Plan!.id_compania == idCompania)   // ← filtro
                .GroupBy(s => new { s.fecha_inicio.Year, s.fecha_inicio.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    total = g.Sum(s => s.Plan!.precio)
                })
                .OrderBy(x => x.year).ThenBy(x => x.month)
                .ToListAsync();

            double maxVal = datos.Any() ? (double)datos.Max(d => d.total) : 1;
            var meses = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

            var resultado = datos.Select(d => new
            {
                month = meses[d.month - 1],
                total = d.total,
                pct = maxVal > 0 ? Math.Round((double)d.total / maxVal * 100, 1) : 0,
                active = d.year == hoy.Year && d.month == hoy.Month
            });

            return Ok(resultado);
        }

        // GET api/suscripcion/recientes?idCompania=1
        [HttpGet("recientes")]
        public async Task<IActionResult> GetRecientes([FromQuery] int idCompania)
        {
            if (idCompania == 0)
                return BadRequest(new { message = "idCompania es obligatorio" });

            var cuotas = await _context.Suscripcion
                .Include(s => s.Cliente).ThenInclude(c => c!.Persona)
                .Include(s => s.Plan)
                .Where(s => s.Plan!.id_compania == idCompania)   // ← filtro
                .OrderByDescending(s => s.fecha_inicio)
                .Take(10)
                .Select(s => new
                {
                    id = s.id_suscripcion,
                    nombre = s.Cliente!.Persona!.nombre,
                    plan = s.Plan!.nombre,
                    monto = s.Plan!.precio,
                    vence = s.fecha_fin.ToString("dd MMM"),
                    estado = s.estado
                })
                .ToListAsync();

            return Ok(cuotas);
        }
    }
}