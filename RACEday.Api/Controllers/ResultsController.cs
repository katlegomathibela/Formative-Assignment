

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
    public class ResultsController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public ResultsController(RaceDayContext context)
        {
            _context = context;
        }

        // POST: api/events/1/results
        [Authorize(Roles = "ORGANISER")]
        [HttpPost("events/{eventId:int}/results")]
        public async Task<IActionResult> CreateResult(
            int eventId,
            [FromBody] CreateResultDto dto)
        {
            var organiserId = CurrentUserId!.Value;

            var raceEvent = await _context.Events
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

            if (dto.FinishPosition <= 0)
            {
                return BadRequest(new
                {
                    message = "Finish position must be greater than zero."
                });
            }

            if (dto.FinishTime < TimeSpan.Zero ||
                dto.FinishTime >= TimeSpan.FromDays(1))
            {
                return BadRequest(new
                {
                    message = "Finish time must be between 00:00:00 and 23:59:59."
                });
            }

            var enrolment = await _context.EventEnrollments
                .FirstOrDefaultAsync(e =>
                    e.EnrollmentId == dto.EnrollmentId &&
                    e.EventId == eventId);

            if (enrolment == null)
            {
                return NotFound(new
                {
                    message = "The enrolment was not found for this event."
                });
            }

            if (enrolment.Status == "CANCELLED")
            {
                return BadRequest(new
                {
                    message = "A result cannot be recorded for a cancelled enrolment."
                });
            }

            var resultExists = await _context.Results
                .AnyAsync(r => r.EnrollmentId == dto.EnrollmentId);

            if (resultExists)
            {
                return Conflict(new
                {
                    message = "A result has already been recorded for this enrolment."
                });
            }

            var result = new Result
            {
                EnrollmentId = dto.EnrollmentId,
                FinishTime = dto.FinishTime,
                FinishPosition = dto.FinishPosition
            };

            _context.Results.Add(result);

            enrolment.Status = "COMPLETED";

            await _context.SaveChangesAsync();

            return Created(
                $"/api/events/{eventId}/results",
                new
                {
                    message = "Result recorded successfully.",
                    result
                });
        }

        // GET: api/users/me/results
        [Authorize(Roles = "PARTICIPANT")]
        [HttpGet("users/me/results")]
        public async Task<IActionResult> GetMyResults()
        {
            var participantId = CurrentUserId!.Value;

            var results = await (
                from r in _context.Results.AsNoTracking()
                join e in _context.EventEnrollments.AsNoTracking()
                    on r.EnrollmentId equals e.EnrollmentId
                join ev in _context.Events.AsNoTracking()
                    on e.EventId equals ev.EventId
                where e.ParticipantId == participantId
                orderby ev.EventDate descending
                select new
                {
                    r.ResultId,
                    r.EnrollmentId,
                    e.EventId,
                    EventName = ev.Name,
                    ev.EventDate,
                    ev.Location,
                    r.FinishTime,
                    r.FinishPosition,
                    e.Status
                })
                .ToListAsync();

            return Ok(results);
        }

        // GET: api/events/1/results
        [Authorize(Roles = "ORGANISER,PARTICIPANT")]
        [HttpGet("events/{eventId:int}/results")]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
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

            var results = await (
                from r in _context.Results.AsNoTracking()
                join e in _context.EventEnrollments.AsNoTracking()
                    on r.EnrollmentId equals e.EnrollmentId
                join u in _context.Users.AsNoTracking()
                    on e.ParticipantId equals u.UserId
                join c in _context.Categories.AsNoTracking()
                    on e.CategoryId equals c.CategoryId
                where e.EventId == eventId
                orderby r.FinishPosition
                select new
                {
                    r.ResultId,
                    r.EnrollmentId,
                    ParticipantId = u.UserId,
                    ParticipantFirstName = u.FirstName,
                    ParticipantLastName = u.LastName,
                    CategoryName = c.Name,
                    r.FinishTime,
                    r.FinishPosition
                })
                .ToListAsync();

            return Ok(new
            {
                eventDetails = new
                {
                    raceEvent.EventId,
                    raceEvent.Name,
                    raceEvent.EventDate,
                    raceEvent.Location,
                    raceEvent.Distance,
                    raceEvent.EventType
                },
                results
            });
        }
    }
}
