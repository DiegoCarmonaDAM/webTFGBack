using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;
using webTFGBack.Models;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClienteController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ClienteController(AppDbContext context) => _context = context;

        // GET api/cliente/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            return Ok(new
            {
                id_cliente = cliente.id_cliente,
                nombre = cliente.Persona!.nombre,
                email = cliente.Persona!.email,
                documento = cliente.Persona!.documento_identidad,
                telefono = cliente.Persona!.telefono,
                suscripciones = cliente.Suscripciones
                    .OrderByDescending(s => s.fecha_inicio)
                    .Select(s => new
                    {
                        id = s.id_suscripcion,
                        plan = s.Plan!.nombre,
                        precio = s.Plan!.precio,
                        fecha_inicio = s.fecha_inicio.ToString("dd/MM/yyyy"),
                        fecha_fin = s.fecha_fin.ToString("dd/MM/yyyy"),
                        accesos = s.accesos_restantes,
                        estado = s.estado,
                        dias_restantes = s.fecha_fin >= hoy ? s.fecha_fin.DayNumber - hoy.DayNumber : 0
                    })
                    .ToList()
            });
        }

        // GET api/cliente/buscar?q=texto&id_gym=1
        [HttpGet("buscar")]
        public async Task<IActionResult> Buscar([FromQuery] string q, [FromQuery] int? id_gym)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { message = "Parámetro de búsqueda vacío" });

            int? id_compania = null;
            if (id_gym.HasValue)
            {
                var gym = await _context.Gym.FindAsync(id_gym.Value);
                id_compania = gym?.id_compania;
            }

            var query = _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .Where(c =>
                    c.Persona!.nombre.Contains(q) ||
                    c.Persona!.documento_identidad.Contains(q) ||
                    (c.Persona!.email != null && c.Persona.email.Contains(q)));

            if (id_compania.HasValue)
                query = query.Where(c => c.Suscripciones.Any(s =>
                    s.estado == "activa" && s.Plan!.id_compania == id_compania.Value));

            var resultados = await query
                .Select(c => new
                {
                    id_cliente = c.id_cliente,
                    nombre = c.Persona!.nombre,
                    email = c.Persona!.email,
                    documento = c.Persona!.documento_identidad,
                    telefono = c.Persona!.telefono,
                    plan_activo = c.Suscripciones
                        .Where(s => s.estado == "activa")
                        .OrderByDescending(s => s.fecha_inicio)
                        .Select(s => s.Plan!.nombre)
                        .FirstOrDefault()
                })
                .Take(10)
                .ToListAsync();

            return Ok(resultados);
        }

        // GET api/cliente/gym/{id_gym}
        [HttpGet("gym/{id_gym}")]
        public async Task<IActionResult> GetByGym(
            int id_gym,
            [FromQuery] string? filtro,
            [FromQuery] string? q,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamano = 20)
        {
            var gym = await _context.Gym.FindAsync(id_gym);
            if (gym == null)
                return NotFound(new { message = "Gimnasio no encontrado" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
               .Where(c => c.Suscripciones.Any(s => s.Plan!.id_compania == gym.id_compania)
                 || !c.Suscripciones.Any())
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c =>
                    c.Persona!.nombre.Contains(q) ||
                    c.Persona!.documento_identidad.Contains(q));

            var todos = await query
                .OrderBy(c => c.Persona!.nombre)
                .Select(c => new
                {
                    id_cliente = c.id_cliente,
                    nombre = c.Persona!.nombre,
                    documento = c.Persona!.documento_identidad,
                    email = c.Persona!.email,
                    telefono = c.Persona!.telefono,
                    plan_activo = c.Suscripciones
                        .Where(s => s.estado == "activa"
                                 && s.fecha_fin >= hoy
                                 && s.Plan!.id_compania == gym.id_compania)  
                        .OrderByDescending(s => s.fecha_fin)
                        .Select(s => s.Plan!.nombre)
                        .FirstOrDefault(),
                    fecha_fin = c.Suscripciones
                        .Where(s => s.estado == "activa"
                                 && s.fecha_fin >= hoy
                                 && s.Plan!.id_compania == gym.id_compania)  // ← filtro compañía
                        .OrderByDescending(s => s.fecha_fin)
                        .Select(s => (DateOnly?)s.fecha_fin)
                        .FirstOrDefault(),
                    dias_restantes = c.Suscripciones
                        .Where(s => s.estado == "activa"
                                 && s.fecha_fin >= hoy
                                 && s.Plan!.id_compania == gym.id_compania)  // ← filtro compañía
                        .OrderByDescending(s => s.fecha_fin)
                        .Select(s => (int?)(s.fecha_fin.DayNumber - hoy.DayNumber))
                        .FirstOrDefault(),
                    tiene_activa = c.Suscripciones
                        .Any(s => s.estado == "activa"
                               && s.fecha_fin >= hoy
                               && s.Plan!.id_compania == gym.id_compania)    // ← filtro compañía
                })
                .ToListAsync();

            var filtrados = filtro switch
            {
                "activos" => todos.Where(c => c.tiene_activa).ToList(),
                "inactivos" => todos.Where(c => !c.tiene_activa).ToList(),
                _ => todos
            };

            var total = filtrados.Count;
            var pagados = filtrados
                .Skip((pagina - 1) * tamano)
                .Take(tamano)
                .Select(c => new
                {
                    c.id_cliente,
                    c.nombre,
                    c.documento,
                    c.email,
                    c.telefono,
                    c.plan_activo,
                    c.tiene_activa,
                    c.dias_restantes,
                    fecha_fin = c.fecha_fin?.ToString("dd/MM/yyyy"),
                    vence_pronto = c.tiene_activa && c.dias_restantes.HasValue && c.dias_restantes <= 7
                })
                .ToList();

            return Ok(new
            {
                total,
                pagina,
                tamano,
                paginas = (int)Math.Ceiling((double)total / tamano),
                clientes = pagados
            });
        }

        // POST api/cliente
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearClienteRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.pass))
                return BadRequest(new { message = "La contraseña es obligatoria" });

            bool existe = await _context.Persona
                .AnyAsync(p => p.documento_identidad == req.documento_identidad ||
                               (req.email != null && p.email == req.email));
            if (existe)
                return Conflict(new { message = "Ya existe una persona con ese documento o email" });

            var persona = new Persona
            {
                nombre = req.nombre,
                email = req.email,
                documento_identidad = req.documento_identidad,
                telefono = req.telefono,
                pass = req.pass
            };
            _context.Persona.Add(persona);
            await _context.SaveChangesAsync();

            var cliente = new Cliente { id_persona = persona.id_persona };
            _context.Cliente.Add(cliente);
            await _context.SaveChangesAsync();

            if (req.id_plan.HasValue)
            {
                var plan = await _context.Plan.FindAsync(req.id_plan.Value);
                if (plan != null)
                {
                    _context.Suscripcion.Add(new Suscripcion
                    {
                        id_cliente = cliente.id_cliente,
                        id_plan = plan.id_plan,
                        fecha_inicio = DateOnly.FromDateTime(DateTime.Today),
                        fecha_fin = DateOnly.FromDateTime(DateTime.Today.AddDays(plan.duracion_dias)),
                        accesos_restantes = plan.total_accesos,
                        estado = "activa"
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Cliente creado correctamente", id_cliente = cliente.id_cliente });
        }

        // PUT api/cliente/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Modificar(int id, [FromBody] ModificarClienteRequest req)
        {
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            if (!string.IsNullOrWhiteSpace(req.nombre)) cliente.Persona!.nombre = req.nombre;
            if (!string.IsNullOrWhiteSpace(req.email)) cliente.Persona!.email = req.email;
            if (!string.IsNullOrWhiteSpace(req.telefono)) cliente.Persona!.telefono = req.telefono;
            if (!string.IsNullOrWhiteSpace(req.pass)) cliente.Persona!.pass = req.pass;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cliente actualizado" });
        }

        // POST api/cliente/{id}/renovar
        [HttpPost("{id}/renovar")]
        public async Task<IActionResult> Renovar(int id, [FromBody] RenovarRequest req)
        {
            var cliente = await _context.Cliente.FindAsync(id);
            if (cliente == null) return NotFound(new { message = "Cliente no encontrado" });

            var plan = await _context.Plan.FindAsync(req.id_plan);
            if (plan == null) return NotFound(new { message = "Plan no encontrado" });

            _context.Suscripcion.Add(new Suscripcion
            {
                id_cliente = cliente.id_cliente,
                id_plan = plan.id_plan,
                fecha_inicio = DateOnly.FromDateTime(DateTime.Today),
                fecha_fin = DateOnly.FromDateTime(DateTime.Today.AddDays(plan.duracion_dias)),
                accesos_restantes = plan.total_accesos,
                estado = "activa"
            });
            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción renovada correctamente" });
        }

        // GET /api/cliente/perfil/{id}
        [HttpGet("perfil/{id}")]
        public async Task<IActionResult> GetPerfilApp(int id)
        {
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var sus = cliente.Suscripciones
                .Where(s => s.estado == "activa" && s.fecha_fin >= hoy)
                .OrderByDescending(s => s.fecha_fin)
                .FirstOrDefault();

            object? suscripcionActiva = null;
            if (sus != null)
                suscripcionActiva = new
                {
                    id_suscripcion = sus.id_suscripcion,
                    plan_nombre = sus.Plan!.nombre,
                    plan_tipo = sus.Plan!.tipo,
                    precio = sus.Plan!.precio,
                    fecha_inicio = sus.fecha_inicio.ToString("dd/MM/yyyy"),
                    fecha_fin = sus.fecha_fin.ToString("dd/MM/yyyy"),
                    accesos_restantes = sus.accesos_restantes,
                    estado = sus.estado
                };

            return Ok(new
            {
                id_cliente = cliente.id_cliente,
                nombre = cliente.Persona!.nombre,
                email = cliente.Persona!.email,
                telefono = cliente.Persona!.telefono,
                documento = cliente.Persona!.documento_identidad,
                suscripcion_activa = suscripcionActiva
            });
        }

        // GET /api/cliente/{id}/entradas
        [HttpGet("{id}/entradas")]
        public async Task<IActionResult> GetEntradasApp(int id)
        {
            var cliente = await _context.Cliente.FindAsync(id);
            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var entradas = await _context.RegistroEntrada
                .Where(r => r.id_persona == cliente.id_persona)
                .OrderByDescending(r => r.fecha_hora_entrada)
                .Take(20)
                .Select(r => new
                {
                    id_registro = r.id_registro,
                    fecha_hora_entrada = r.fecha_hora_entrada.ToString("dd/MM/yyyy HH:mm"),
                    fecha_hora_salida = r.fecha_hora_salida.HasValue
                                          ? r.fecha_hora_salida.Value.ToString("dd/MM/yyyy HH:mm")
                                          : null
                })
                .ToListAsync();

            return Ok(entradas);
        }

        // GET /api/cliente/{id}/suscripciones
        [HttpGet("{id}/suscripciones")]
        public async Task<IActionResult> GetSuscripcionesPorDni(int id)
        {
            // 1. Obtener el DNI del cliente logueado
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var dni = cliente.Persona!.documento_identidad;

            // 2. Buscar todos los id_cliente que tengan ese mismo DNI
            var idClientes = await _context.Cliente
                .Where(c => c.Persona!.documento_identidad == dni)
                .Select(c => c.id_cliente)
                .ToListAsync();

            // 3. Obtener todas sus suscripciones con nombre de gimnasio (Compania)
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var suscripciones = await _context.Suscripcion
                .Include(s => s.Plan).ThenInclude(p => p!.Compania)
                .Where(s => idClientes.Contains(s.id_cliente))
                .OrderByDescending(s => s.fecha_inicio)
                .Select(s => new
                {
                    id = s.id_suscripcion,
                    plan = s.Plan!.nombre,
                    tipo = s.Plan!.tipo,
                    precio = s.Plan!.precio,
                    gimnasio = s.Plan!.Compania!.nombre,
                    fecha_inicio = s.fecha_inicio.ToString("dd/MM/yyyy"),
                    fecha_fin = s.fecha_fin.ToString("dd/MM/yyyy"),
                    accesos = s.accesos_restantes,
                    estado = s.estado,
                    dias_restantes = s.fecha_fin >= hoy
                                     ? s.fecha_fin.DayNumber - hoy.DayNumber
                                     : 0
                })
                .ToListAsync();

            return Ok(suscripciones);
        }
    }

    public class CrearClienteRequest
    {
        public string nombre { get; set; } = string.Empty;
        public string documento_identidad { get; set; } = string.Empty;
        public string pass { get; set; } = string.Empty;
        public string? email { get; set; }
        public string? telefono { get; set; }
        public int? id_plan { get; set; }
    }

    public class ModificarClienteRequest
    {
        public string? nombre { get; set; }
        public string? email { get; set; }
        public string? telefono { get; set; }
        public string? pass { get; set; }
    }

    public class RenovarRequest { public int id_plan { get; set; } }
}