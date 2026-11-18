using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
namespace GestionCitas.Api.Services;

public static class PrescriptionContent
{
    public static IReadOnlyList<PrescriptionItemDto> Items(Prescription row) => row.Items.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
        .Select(x => new PrescriptionItemDto(x.Medication, x.Dose, x.Frequency, x.Duration, x.Instructions)).ToList();
    public static string Snapshot(Prescription row) => JsonSerializer.Serialize(new { row.GeneralIndications, row.IsActive, Items = Items(row) });
    public static string Version(string snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
}
