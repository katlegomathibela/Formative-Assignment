
namespace RaceDay.Api.Models;

public class Route
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public decimal DistanceKm { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string FinishLocation { get; set; } = string.Empty;
    public string? RouteData { get; set; }
}
