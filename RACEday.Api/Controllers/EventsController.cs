

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public EventsController(RaceDayContext context)
        {
            _context = context;
        }

        // GET: api/events
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAllEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
                .ToListAsync();

            return Ok(events);
        }

        // GET: api/events/5
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetEventById(int id)
        {
            var raceEvent = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            return Ok(raceEvent);
        }

        // POST: api/events
        [Authorize(Roles = "ORGANISER")]
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) ||
                string.IsNullOrWhiteSpace(dto.Description) ||
                string.IsNullOrWhiteSpace(dto.Location))
            {
                return BadRequest(new
                {
                    message = "Name, description and location are required."
                });
            }

            if (dto.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "Distance must be greater than zero."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.EventType))
            {
                return BadRequest(new
                {
                    message = "Event type is required."
                });
            }

            var eventType = dto.EventType.Trim().ToUpperInvariant();
            var validTypes = new[] { "RUN", "WALK", "CYCLE" };

            if (!validTypes.Contains(eventType))
            {
                return BadRequest(new
                {
                    message = "Event type must be RUN, WALK or CYCLE."
                });
            }

            var raceEvent = new Event
            {
                OrganiserId = CurrentUserId!.Value,
                Name = dto.Name.Trim(),
                Description = dto.Description.Trim(),
                EventDate = dto.EventDate,
                Location = dto.Location.Trim(),
                Distance = dto.Distance,
                EventType = eventType
            };

            _context.Events.Add(raceEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetEventById),
                new { id = raceEvent.EventId },
                raceEvent);
        }

        // PUT: api/events/5
        [Authorize(Roles = "ORGANISER")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateEvent(
            int id,
            [FromBody] CreateEventDto dto)
        {
            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (raceEvent.OrganiserId != CurrentUserId!.Value)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(dto.Name) ||
                string.IsNullOrWhiteSpace(dto.Description) ||
                string.IsNullOrWhiteSpace(dto.Location))
            {
                return BadRequest(new
                {
                    message = "Name, description and location are required."
                });
            }

            if (dto.Distance <= 0)
            {
                return BadRequest(new
                {
                    message = "Distance must be greater than zero."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.EventType))
            {
                return BadRequest(new
                {
                    message = "Event type is required."
                });
            }

            var eventType = dto.EventType.Trim().ToUpperInvariant();
            var validTypes = new[] { "RUN", "WALK", "CYCLE" };

            if (!validTypes.Contains(eventType))
            {
                return BadRequest(new
                {
                    message = "Event type must be RUN, WALK or CYCLE."
                });
            }

            raceEvent.Name = dto.Name.Trim();
            raceEvent.Description = dto.Description.Trim();
            raceEvent.EventDate = dto.EventDate;
            raceEvent.Location = dto.Location.Trim();
            raceEvent.Distance = dto.Distance;
            raceEvent.EventType = eventType;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event updated successfully.",
                eventDetails = raceEvent
            });
        }

        // DELETE: api/events/5
        [Authorize(Roles = "ORGANISER")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (raceEvent.OrganiserId != CurrentUserId!.Value)
            {
                return Forbid();
            }

            var hasEnrolments = await _context.EventEnrollments
                .AnyAsync(e => e.EventId == id);

            var hasCategories = await _context.Categories
                .AnyAsync(c => c.EventId == id);

            var hasRoute = await _context.Routes
                .AnyAsync(r => r.EventId == id);

            if (hasEnrolments || hasCategories || hasRoute)
            {
                return Conflict(new
                {
                    message = "This event cannot be deleted while it has enrolments, categories or a route."
                });
            }

            _context.Events.Remove(raceEvent);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Event deleted successfully."
            });
        }
    }
}

