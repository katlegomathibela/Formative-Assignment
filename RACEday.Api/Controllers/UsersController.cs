
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly RaceDayContext _context;

        public UsersController(RaceDayContext context)
        {
            _context = context;
        }

        // GET: api/users/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = CurrentUserId!.Value;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User profile not found."
                });
            }

            return Ok(user);
        }

        // PUT: api/users/me
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateProfileDto dto)
        {
            var userId = CurrentUserId!.Value;

            if (string.IsNullOrWhiteSpace(dto.FirstName) ||
                string.IsNullOrWhiteSpace(dto.LastName) ||
                string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new
                {
                    message = "First name, last name and email are required."
                });
            }

            if (dto.FirstName.Trim().Length > 50 ||
                dto.LastName.Trim().Length > 50 ||
                dto.Email.Trim().Length > 100)
            {
                return BadRequest(new
                {
                    message = "One or more fields exceed the maximum permitted length."
                });
            }

            var email = dto.Email.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User profile not found."
                });
            }

            var emailInUse = await _context.Users
                .AnyAsync(u =>
                    u.UserId != userId &&
                    u.Email.ToLower() == email.ToLower());

            if (emailInUse)
            {
                return Conflict(new
                {
                    message = "That email address is already in use."
                });
            }

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Email = email;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profile updated successfully.",
                profile = new
                {
                    user.UserId,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Role,
                    user.CreatedAt
                }
            });
        }
    }
}
