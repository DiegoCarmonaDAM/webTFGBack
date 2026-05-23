using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GymController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GymController(AppDbContext context) => _context = context;

        // GET api/gym/info?id_trabajador=1
        // Devuelve el gym del trabajador logueado
        [HttpGet("info")]
        public async Task<IActionResult> GetInfo([FromQuery] int? id_trabajador)
        {
            var gymQuery = _context.Gym.Include(g => g.Compania).AsQueryable();

            if (id_trabajador.HasValue)
            {
                var trabajador = await _context.Trabajador
                    .FirstOrDefaultAsync(t => t.id_trabajador == id_trabajador.Value);

                if (trabajador == null)
                    return NotFound(new { message = "Trabajador no encontrado" });

                gymQuery = gymQuery.Where(g => g.id_gym == trabajador.id_gym);
            }

            var gym = await gymQuery
                .Select(g => new
                {
                    id_gym = g.id_gym,
                    nombre = g.nombre,
                    ciudad = g.ciudad,
                    compania = g.Compania!.nombre,
                    activosHoy = _context.RegistroEntrada.Count(r =>
                        r.id_gym == g.id_gym &&
                        r.fecha_hora_entrada >= DateTime.Today &&
                        r.fecha_hora_salida == null)
                })
                .FirstOrDefaultAsync();

            if (gym == null)
                return NotFound(new { message = "No se encontró el gimnasio del trabajador" });

            return Ok(gym);
        }
    }
}
