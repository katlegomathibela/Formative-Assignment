

using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class AuthControllerTests
    {
        private static IConfiguration CreateConfiguration()
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Key"] =
                    "RaceDay-Test-Key-That-Is-Long-Enough-For-Testing-2026!",
                ["Jwt:Issuer"] = "RaceDay.Api",
                ["Jwt:Audience"] = "RaceDay.Client",
                ["Jwt:ExpiryMinutes"] = "60"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static RaceDayContext CreateContext(
            SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<RaceDayContext>()
                .UseSqlite(connection)
                .Options;

            var context = new RaceDayContext(options);
            context.Database.EnsureCreated();

            return context;
        }

        private static async Task<User> CreateTestUser(
            RaceDayContext context,
            string email = "participant@example.com",
            string password = "TestPassword123!")
        {
            var user = new User
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = email,
                Role = "PARTICIPANT",
                CreatedAt = DateTime.Now
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, password);

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return user;
        }

        [Fact]
        public async Task Register_ValidDetails_CreatesParticipantWithHashedPassword()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new RegisterDto
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "participant@example.com",
                Password = "TestPassword123!"
            };

            var result = await controller.Register(dto);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, response.StatusCode);

            var user = await context.Users.SingleAsync();

            Assert.Equal("Test", user.FirstName);
            Assert.Equal("Participant", user.LastName);
            Assert.Equal("participant@example.com", user.Email);
            Assert.Equal("PARTICIPANT", user.Role);
            Assert.NotEqual(dto.Password, user.PasswordHash);

            var hasher = new PasswordHasher<User>();
            var verification = hasher.VerifyHashedPassword(
                user, user.PasswordHash, dto.Password);

            Assert.NotEqual(
                PasswordVerificationResult.Failed, verification);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new RegisterDto
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "duplicate@example.com",
                Password = "TestPassword123!"
            };

            await controller.Register(dto);
            var result = await controller.Register(dto);

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Single(context.Users);
        }

        [Fact]
        public async Task Register_ShortPassword_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new RegisterDto
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "shortpassword@example.com",
                Password = "short"
            };

            var result = await controller.Register(dto);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.Users);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsJwtToken()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            await CreateTestUser(context);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new LoginDto
            {
                Email = "participant@example.com",
                Password = "TestPassword123!"
            };

            var result = await controller.Login(dto);

            var response = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(response.Value);

            var tokenProperty = response.Value.GetType()
                .GetProperty("token", BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(tokenProperty);

            var token = tokenProperty!.GetValue(response.Value) as string;
            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task Login_IncorrectPassword_ReturnsUnauthorized()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            await CreateTestUser(context);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new LoginDto
            {
                Email = "participant@example.com",
                Password = "WrongPassword123!"
            };

            var result = await controller.Login(dto);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsUnauthorized()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var controller = new AuthController(
                context, CreateConfiguration());

            var dto = new LoginDto
            {
                Email = "unknown@example.com",
                Password = "TestPassword123!"
            };

            var result = await controller.Login(dto);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}

