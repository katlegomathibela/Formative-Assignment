
namespace RaceDay.Api.DTOs
{
    public class CreateResultDto
    {
        public int EnrollmentId { get; set; }

        public TimeSpan FinishTime { get; set; }

        public int FinishPosition { get; set; }
    }
}
