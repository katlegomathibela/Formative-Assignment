
namespace RaceDay.Api.Models;

public class EventEnrollment
{
    public int EnrollmentId { get; set; }
    public int EventId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string Status { get; set; } = "REGISTERED";
}
