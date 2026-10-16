
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class EnrolmentsController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public EnrolmentsController(RaceDayContext context)
        {
            _context = context;
        }

        // POST: api/events/1/enrolments
        [Authorize(Roles = "PARTICIPANT")]
        [HttpPost("events/{eventId:int}/enrolments")]
        public async Task<IActionResult> CreateEnrolment(
            int eventId,
            [FromBody] CreateEnrolmentDto dto)
        {
            var participantId = CurrentUserId!.Value;

            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (raceEvent.EventDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new
                {
                    message = "You cannot enrol in an event that has already passed."
                });
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryId == dto.CategoryId &&
                    c.EventId == eventId);

            if (category == null)
            {
                return BadRequest(new
                {
                    message = "The selected category does not belong to this event."
                });
            }

            var existingEnrolment = await _context.EventEnrollments
                .FirstOrDefaultAsync(e =>
                    e.EventId == eventId &&
                    e.ParticipantId == participantId);

            if (existingEnrolment != null)
            {
                if (existingEnrolment.Status != "CANCELLED")
                {
                    return Conflict(new
                    {
                        message = "You are already enrolled in this event."
                    });
                }

                existingEnrolment.CategoryId = dto.CategoryId;
                existingEnrolment.Status = "REGISTERED";
                existingEnrolment.RegistrationDate = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Your enrolment has been restored successfully.",
                    enrolment = existingEnrolment
                });
            }

            var enrolment = new EventEnrollment
            {
                EventId = eventId,
                ParticipantId = participantId,
                CategoryId = dto.CategoryId,
                RegistrationDate = DateTime.Now,
                Status = "REGISTERED"
            };

            _context.EventEnrollments.Add(enrolment);
            await _context.SaveChangesAsync();

            return Created(
                $"/api/events/{eventId}/enrolments",
                enrolment);
        }

        // GET: api/users/me/enrolments
        [Authorize(Roles = "PARTICIPANT")]
        [HttpGet("users/me/enrolments")]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var participantId = CurrentUserId!.Value;

            var enrolments = await (
                from e in _context.EventEnrollments.AsNoTracking()
                join ev in _context.Events.AsNoTracking()
                    on e.EventId equals ev.EventId
                join c in _context.Categories.AsNoTracking()
                    on e.CategoryId equals c.CategoryId
                where e.ParticipantId == participantId
                orderby ev.EventDate
                select new
                {
                    e.EnrollmentId,
                    e.EventId,
                    EventName = ev.Name,
                    ev.EventDate,
                    ev.Location,
                    e.CategoryId,
                    CategoryName = c.Name,
                    e.RegistrationDate,
                    e.Status
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        // DELETE: api/events/1/enrolments
        [Authorize(Roles = "PARTICIPANT")]
        [HttpDelete("events/{eventId:int}/enrolments")]
        public async Task<IActionResult> CancelEnrolment(int eventId)
        {
            var participantId = CurrentUserId!.Value;

            var enrolment = await _context.EventEnrollments
                .FirstOrDefaultAsync(e =>
                    e.EventId == eventId &&
                    e.ParticipantId == participantId);

            if (enrolment == null)
            {
                return NotFound(new
                {
                    message = "You are not enrolled in this event."
                });
            }

            if (enrolment.Status == "CANCELLED")
            {
                return BadRequest(new
                {
                    message = "Your enrolment has already been cancelled."
                });
            }

            if (enrolment.Status == "COMPLETED")
            {
                return BadRequest(new
                {
                    message = "You cannot cancel an enrolment that has been completed."
                });
            }

            enrolment.Status = "CANCELLED";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Your enrolment has been cancelled successfully."
            });
        }

        // GET: api/events/1/enrolments
        [Authorize(Roles = "ORGANISER")]
        [HttpGet("events/{eventId:int}/enrolments")]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var organiserId = CurrentUserId!.Value;

            var raceEvent = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (raceEvent.OrganiserId != organiserId)
            {
                return Forbid();
            }

            var enrolments = await (
                from e in _context.EventEnrollments.AsNoTracking()
                join u in _context.Users.AsNoTracking()
                    on e.ParticipantId equals u.UserId
                join c in _context.Categories.AsNoTracking()
                    on e.CategoryId equals c.CategoryId
                where e.EventId == eventId
                orderby e.RegistrationDate
                select new
                {
                    e.EnrollmentId,
                    e.EventId,
                    e.ParticipantId,
                    ParticipantFirstName = u.FirstName,
                    ParticipantLastName = u.LastName,
                    ParticipantEmail = u.Email,
                    e.CategoryId,
                    CategoryName = c.Name,
                    e.RegistrationDate,
                    e.Status
                })
                .ToListAsync();

            return Ok(enrolments);
        }
    }
}


