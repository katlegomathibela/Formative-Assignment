

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
    public class CategoriesController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public CategoriesController(RaceDayContext context)
        {
            _context = context;
        }

        // GET: api/events/1/categories
        [AllowAnonymous]
        [HttpGet("events/{eventId:int}/categories")]
        public async Task<IActionResult> GetCategories(int eventId)
        {
            var eventExists = await _context.Events
                .AnyAsync(e => e.EventId == eventId);

            if (!eventExists)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.EventId == eventId)
                .ToListAsync();

            return Ok(categories);
        }

        // POST: api/events/1/categories
        [Authorize(Roles = "ORGANISER")]
        [HttpPost("events/{eventId:int}/categories")]
        public async Task<IActionResult> CreateCategory(
            int eventId,
            [FromBody] CreateCategoryDto dto)
        {
            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == eventId);

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

            var validationError = ValidateCategory(dto);

            if (validationError != null)
            {
                return BadRequest(new
                {
                    message = validationError
                });
            }

            var categoryName = dto.Name.Trim();

            var duplicateExists = await _context.Categories
                .AnyAsync(c =>
                    c.EventId == eventId &&
                    c.Name == categoryName);

            if (duplicateExists)
            {
                return Conflict(new
                {
                    message = "A category with this name already exists for this event."
                });
            }

            var category = new Category
            {
                EventId = eventId,
                Name = categoryName,
                CategoryType = dto.CategoryType.Trim().ToUpperInvariant(),
                Value = dto.Value.Trim()
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Created(
                $"/api/categories/{category.CategoryId}",
                category);
        }

        // PUT: api/categories/1
        [Authorize(Roles = "ORGANISER")]
        [HttpPut("categories/{id:int}")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            [FromBody] CreateCategoryDto dto)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }

            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == category.EventId);

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

            var validationError = ValidateCategory(dto);

            if (validationError != null)
            {
                return BadRequest(new
                {
                    message = validationError
                });
            }

            var categoryName = dto.Name.Trim();

            var duplicateExists = await _context.Categories
                .AnyAsync(c =>
                    c.EventId == category.EventId &&
                    c.CategoryId != id &&
                    c.Name == categoryName);

            if (duplicateExists)
            {
                return Conflict(new
                {
                    message = "A category with this name already exists for this event."
                });
            }

            category.Name = categoryName;
            category.CategoryType =
                dto.CategoryType.Trim().ToUpperInvariant();
            category.Value = dto.Value.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Category updated successfully.",
                category
            });
        }

        // DELETE: api/categories/1
        [Authorize(Roles = "ORGANISER")]
        [HttpDelete("categories/{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return NotFound(new
                {
                    message = "Category not found."
                });
            }

            var raceEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == category.EventId);

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

            var categoryIsInUse = await _context.EventEnrollments
                .AnyAsync(e => e.CategoryId == id);

            if (categoryIsInUse)
            {
                return Conflict(new
                {
                    message = "This category cannot be deleted because participants are enrolled in it."
                });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Category deleted successfully."
            });
        }

        private static string? ValidateCategory(CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return "Category name is required.";
            }

            if (dto.Name.Trim().Length > 50)
            {
                return "Category name cannot exceed 50 characters.";
            }

            if (string.IsNullOrWhiteSpace(dto.CategoryType))
            {
                return "Category type is required.";
            }

            var categoryType = dto.CategoryType.Trim().ToUpperInvariant();

            if (categoryType != "AGE" && categoryType != "DISTANCE")
            {
                return "Category type must be AGE or DISTANCE.";
            }

            if (string.IsNullOrWhiteSpace(dto.Value))
            {
                return "Category value is required.";
            }

            if (dto.Value.Trim().Length > 50)
            {
                return "Category value cannot exceed 50 characters.";
            }

            return null;
        }
    }
}
