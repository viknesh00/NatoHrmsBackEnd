using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using BC = BCrypt.Net.BCrypt;

namespace NatoHrmsBackend.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AuthController : ControllerBase
	{
		private readonly ApplicationDbContext _context;
		private readonly IConfiguration _config;

		public AuthController(ApplicationDbContext context, IConfiguration config)
		{
			_context = context;
			_config = config;
		}

		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] User user)
		{
			if (user == null || string.IsNullOrEmpty(user.Email))
				return BadRequest("Invalid user data");

			try
			{
				// ✅ Step 1: Save User
				_context.Users.Add(user);
				await _context.SaveChangesAsync();

				// ✅ Step 2: Create Login
				var defaultPassword = "Natobotics@123";
				var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

				var userLogin = new UserLogin
				{
					UserId = user.UserId, // must match inserted user
					UserName = user.Email, // you can use EmployeeId if preferred
					PasswordHash = passwordHash,
					IsActive = true,
					IsDefaultPasswordChanged = false,
					CreatedAt = DateTime.Now
				};

				_context.UserLogins.Add(userLogin);
				await _context.SaveChangesAsync();

				return Ok(new
				{
					message = "User registered successfully",
					userId = user.UserId,
					defaultPassword = defaultPassword
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Error registering user",
					error = ex.InnerException?.Message ?? ex.Message
				});
			}
		}

		// ✅ LOGIN
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			// Fetch login along with related user in a single query
			var login = _context.UserLogins
								.Include(l => l.User)
								.FirstOrDefault(u => u.UserName == request.UserName);

			if (login == null)
				return Conflict(new { Message = "Invalid email or password." });

			// Check password
			if (!BCrypt.Net.BCrypt.Verify(request.Password, login.PasswordHash))
				return Conflict(new { Message = "Invalid email or password." });

			// Check if user is active
			if (!login.IsActive)
				return Conflict(new { Message = "Your account is inactive. Please contact administrator." });

			// Update last login
			login.LastLoginAt = DateTime.Now;
			await _context.SaveChangesAsync();

			var user = login.User;

			// Call stored procedure to fix missing clock-outs
			await _context.Database.ExecuteSqlRawAsync("EXEC FixMissingClockOutForAll");

			// Get latest attendance where ClockOutAt is null
			var clockIn = _context.Attendance
								  .Where(a => a.UserEmail == user.Email
											  && a.AttendanceDate == DateTime.Today
											  && a.ClockOut == null)
								  .OrderByDescending(a => a.ClockIn)
								  .Select(a => a.ClockIn)
								  .FirstOrDefault(); // returns null if not clocked in today

			// Prepare JWT token
			var authClaims = new[]
			{
		new Claim(ClaimTypes.Name, user.Email),
		new Claim(ClaimTypes.Role, user?.AccessRole ?? "User"),
		new Claim("UserId", login.UserId.ToString()),
		new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
	};

			var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
			var authSigningKey = new SymmetricSecurityKey(key);

			var token = new JwtSecurityToken(
				issuer: _config["Jwt:Issuer"],
				audience: _config["Jwt:Audience"],
				expires: DateTime.Now.AddHours(double.Parse(_config["Jwt:TokenExpiry"])),
				claims: authClaims,
				signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
			);

			return Ok(new
			{
				token = new JwtSecurityTokenHandler().WriteToken(token),
				expiration = token.ValidTo,
				email = user.Email,
				role = user.AccessRole,
				isDefaultPasswordChanged = login.IsDefaultPasswordChanged,
				firstName = user.FirstName,
				lastName = user.LastName,
				employeeId = user.EmployeeId,
				clockIn = clockIn
			});
		}


	}
}
