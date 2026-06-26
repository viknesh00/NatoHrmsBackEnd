using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;
using System.IdentityModel.Tokens.Jwt;
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
		private readonly IEmailService _emailService;
		private readonly IConfiguration _config;

		public AuthController(ApplicationDbContext context, IConfiguration config, IEmailService emailService)
		{
			_context = context;
			_config = config;
			_emailService = emailService;
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

		// POST: api/Auth/login
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			var login = _context.UserLogins
				.Include(l => l.User)
				.FirstOrDefault(u => u.UserName == request.UserName);
			if (login == null)
				return Conflict(new { Message = "Invalid email or password." });
			if (!BC.Verify(request.Password, login.PasswordHash))
				return Conflict(new { Message = "Invalid email or password." });
			if (!login.IsActive)
				return Conflict(new { Message = "Your account is inactive. Please contact administrator." });

			login.LastLoginAt = DateTime.Now;
			await _context.SaveChangesAsync();

			var user = login.User;

			var clockIn = _context.Attendance
				.Where(a => a.UserEmail == user.Email && a.AttendanceDate == DateTime.Today && a.ClockOut == null)
				.OrderByDescending(a => a.ClockIn)
				.Select(a => a.ClockIn)
				.FirstOrDefault();

			// Fetch department timing config for this user's department
			var deptTiming = _context.DepartmentTimings
				.FirstOrDefault(d => d.DepartmentName == user.Department);

			bool includeSaturday = deptTiming?.IncludeSaturday ?? false;
			bool includeSunday = deptTiming?.IncludeSunday ?? false;

			var authClaims = new[]
			{
		new Claim(ClaimTypes.Name, user.Email),
		new Claim(ClaimTypes.Role, user?.AccessRole ?? "User"),
		new Claim("UserId", login.UserId.ToString()),
		new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
	};

			var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
			var token = new JwtSecurityToken(
				issuer: _config["Jwt:Issuer"],
				audience: _config["Jwt:Audience"],
				expires: DateTime.Now.AddHours(double.Parse(_config["Jwt:TokenExpiry"])),
				claims: authClaims,
				signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
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
				clockIn = clockIn,
				department = user.Department,
				includeSaturday = includeSaturday,
				includeSunday = includeSunday,
				shiftStartTime = deptTiming?.StartTime,
				shiftEndTime = deptTiming?.EndTime
			});
		}

		// POST: api/Auth/SendOtp
		[HttpPost("SendOtp")]
		public async Task<IActionResult> SendOtp([FromBody] EmailRequest request)
		{
			if (string.IsNullOrEmpty(request.Email))
				return BadRequest(new { message = "Email is required" });

			var userLogin = await _context.UserLogins.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == request.Email);

			if (userLogin == null)
				return NotFound(new { message = "User not found" });

			var otp = new Random().Next(100000, 999999).ToString();
			userLogin.Otp = otp;
			userLogin.OtpExpiry = DateTime.Now.AddMinutes(10);
			await _context.SaveChangesAsync();

			try
			{
				var body = EmailTemplates.OtpEmail(userLogin.User.FirstName, userLogin.User.LastName, otp);
				await _emailService.SendEmail(userLogin.User.Email, "Your OTP for Password Reset – Natobotics HRMS", body);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Failed to send OTP email", error = ex.Message });
			}

			return Ok(new { message = "OTP sent successfully" });
		}

		// POST: api/Auth/VerifyOtp
		[HttpPost("VerifyOtp")]
		public async Task<IActionResult> VerifyOtp([FromBody] OtpRequest request)
		{
			if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Otp))
				return BadRequest(new { message = "Email and OTP are required" });

			var userLogin = await _context.UserLogins.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == request.Email);

			if (userLogin == null)
				return NotFound(new { message = "User not found" });

			if (userLogin.Otp != request.Otp || userLogin.OtpExpiry < DateTime.Now)
				return BadRequest(new { message = "Invalid or expired OTP" });

			return Ok(new { message = "OTP verified successfully" });
		}

		// POST: api/Auth/ResetPassword
		[HttpPost("ResetPassword")]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
		{
			if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Otp) || string.IsNullOrEmpty(request.NewPassword))
				return BadRequest(new { message = "All fields are required" });

			var userLogin = await _context.UserLogins.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == request.Email);

			if (userLogin == null)
				return NotFound(new { message = "User not found" });

			if (userLogin.Otp != request.Otp || userLogin.OtpExpiry < DateTime.Now)
				return BadRequest(new { message = "Invalid or expired OTP" });

			userLogin.PasswordHash = BC.HashPassword(request.NewPassword);
			userLogin.IsDefaultPasswordChanged = true;
			userLogin.Otp = null;
			userLogin.OtpExpiry = null;
			await _context.SaveChangesAsync();

			// Send password changed confirmation
			try
			{
				var body = EmailTemplates.PasswordChangedEmail(userLogin.User.FirstName, userLogin.User.LastName);
				await _emailService.SendEmail(userLogin.User.Email, "Password Reset Successful – Natobotics HRMS", body);
			}
			catch { /* Non-blocking */ }

			return Ok(new { message = "Password reset successfully" });
		}
	}
}
