using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-02: administración de profesionales y su especialidad (solo Administrador).</summary>
[ApiController]
[Route("api/admin/professionals")]
[Authorize(Roles = "Administrador")]
public class AdminProfessionalsController(AppDbContext db, DniLookupService dniLookup) : ControllerBase
{
    private const string ProfessionalRole = "Profesional";
    private static readonly PasswordHasher<User> Hasher = new();

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var data = await db.Professionals
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new AdminProfessionalDto(x.Id, x.FullName, x.Office, x.SpecialtyId, x.Specialty.Name, x.Specialty.IsActive, x.IsActive,
                x.User != null ? x.User.Email : null, x.User != null ? x.User.Dni : null))
            .ToListAsync();

        return Ok(data);
    }

    // HU-44: el profesional se registra junto con su cuenta (rol Profesional), en un solo paso.
    // El DNI se valida con el servicio externo; la cuenta guarda el nombre oficial.
    [HttpPost]
    public async Task<IActionResult> Create(CreateProfessionalRequest request, CancellationToken cancellationToken)
    {
        var dni = request.Dni.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(x => x.Dni == dni, cancellationToken))
            return Conflict(new { message = "Ya existe una cuenta con ese DNI." });
        if (await db.Users.AnyAsync(x => x.Email.ToLower() == email, cancellationToken))
            return Conflict(new { message = "Ese correo ya está registrado en otra cuenta." });

        var dniResult = await dniLookup.LookupAsync(dni, cancellationToken);
        if (dniResult.Status == DniLookupStatus.NotFound)
            return BadRequest(new { message = "No encontramos ese DNI. Verifica el número." });
        if (dniResult.Status == DniLookupStatus.Unavailable)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "No pudimos verificar el DNI ahora. Intenta en unos minutos." });

        // La cuenta guarda el nombre oficial del DNI; el profesional puede llevar "Dr."/"Dra." (solo al registrar).
        var officialName = dniResult.FullName!;
        var displayName = string.IsNullOrWhiteSpace(request.FullName) ? officialName : request.FullName.Trim();

        var (specialty, error) = await ValidateAsync(new SaveProfessionalRequest(displayName, request.Office, request.SpecialtyId), null);
        if (error is not null) return BadRequest(new { message = error });

        var user = new User { Dni = dni, Email = email, FullName = officialName, Role = ProfessionalRole, IsActive = true };
        user.PasswordHash = Hasher.HashPassword(user, request.Password);

        var professional = new Professional
        {
            FullName = displayName,
            Office = request.Office?.Trim() ?? string.Empty,
            SpecialtyId = specialty!.Id,
            User = user
        };

        // La cuenta y el profesional se guardan juntos: si algo falla, no queda nada a medias.
        db.Professionals.Add(professional);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Otro administrador registró el mismo DNI o correo al mismo tiempo.
            return Conflict(new { message = "Ya existe una cuenta con ese DNI o correo." });
        }

        return Ok(ToDto(professional, specialty, user));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveProfessionalRequest request)
    {
        var professional = await db.Professionals.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        var (specialty, error) = await ValidateAsync(request, professional);
        if (error is not null) return BadRequest(new { message = error });

        // El nombre solo se define al registrar; al editar no cambia.
        professional.Office = request.Office?.Trim() ?? string.Empty;
        professional.SpecialtyId = specialty!.Id;
        await db.SaveChangesAsync();

        return Ok(ToDto(professional, specialty, professional.User));
    }

    // Desactivar no elimina el registro ni sus citas: solo deja de ofrecerse a los pacientes.
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetActiveRequest request)
    {
        var professional = await db.Professionals
            .Include(x => x.Specialty)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        professional.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Ok(ToDto(professional, professional.Specialty, professional.User));
    }

    private async Task<(Specialty? Specialty, string? Error)> ValidateAsync(SaveProfessionalRequest request, Professional? current)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length < 3) return (null, "El nombre del profesional es obligatorio (mínimo 3 caracteres).");
        if (fullName.Length > 200) return (null, "El nombre no puede superar los 200 caracteres.");
        if ((request.Office?.Trim().Length ?? 0) > 100) return (null, "El consultorio no puede superar los 100 caracteres.");

        var specialty = await db.Specialties.FirstOrDefaultAsync(x => x.Id == request.SpecialtyId);
        if (specialty is null) return (null, "Selecciona una especialidad válida.");

        // Solo se puede asociar a una especialidad activa; si ya pertenecía a una inactiva, se permite editar sus otros datos.
        var changesSpecialty = current is null || current.SpecialtyId != specialty.Id;
        if (changesSpecialty && !specialty.IsActive)
            return (null, "No se puede asociar un profesional a una especialidad inactiva.");

        return (specialty, null);
    }

    private static AdminProfessionalDto ToDto(Professional x, Specialty specialty, User? user) =>
        new(x.Id, x.FullName, x.Office, specialty.Id, specialty.Name, specialty.IsActive, x.IsActive, user?.Email, user?.Dni);
}
