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

			// Fix missing clock-out for all users
			//await _context.Database.ExecuteSqlRawAsync("EXEC FixMissingClockOutForAll");


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

		[HttpPost("SendOtp")]
		public async Task<IActionResult> SendOtp([FromBody] EmailRequest request)
		{
			if (string.IsNullOrEmpty(request.Email))
				return BadRequest(new { message = "Email is required" });

			var userLogin = await _context.UserLogins.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == request.Email);

			if (userLogin == null)
				return NotFound(new { message = "User not found" });

			// Generate 6-digit OTP
			var otp = new Random().Next(100000, 999999).ToString();

			// Save OTP and expiry
			userLogin.Otp = otp;
			userLogin.OtpExpiry = DateTime.Now.AddMinutes(10);
			await _context.SaveChangesAsync();

			try
			{
				string subject = "Your OTP for Password Reset";
				string body = $@"
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
  <tr>
    <td align='center'>
      <table width='600' cellpadding='20' cellspacing='0' border='0' style='border:1px solid #ddd;'>
        <tr>
          <td>
            <h2 style='color:#2E86C1; margin:0 0 10px 0;'>{subject}</h2>
            <p style='margin:5px 0;'>Dear {userLogin.User.FirstName} {userLogin.User.LastName},</p>
            <p style='margin:10px 0;'>Your One-Time Password (OTP) for password reset is:</p>
            <p style='font-size:24px; font-weight:bold; color:#E74C3C; margin:10px 0;'>{otp}</p>
            <p style='margin:10px 0;'>This OTP is valid for <strong>10 minutes</strong>. Please do not share it with anyone.</p>
            <p style='margin:20px 0;'>If you did not request this, please ignore this email.</p>
            <hr style='border:none; border-top:1px solid #ddd; margin:20px 0;' />
            <p style='color:gray; font-size:12px; margin:5px 0;'>This is a system-generated email</p>
            <p style='margin:10px 0;'>Regards,<br/>HRMS Team</p>
            <table cellpadding='0' cellspacing='0' border='0' style='margin-top:10px;'>
              <tr>
                <td>
                  <img src='https://www.natobotics.com/img/Natobotics.png' alt='Company Logo' width='50' style='display:block;' />
                </td>
                <td style='padding-left:10px; font-size:12px; color:#333;'>
                  e: hr@natobotics.com<br/>
                  a: Natobotics Technologies Pvt Ltd, Tidel Park, Taramani, Chennai-600113<br/>
                  w: <a href='https://www.natobotics.com' style='color:#2E86C1; text-decoration:none;'>www.natobotics.com</a>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";


				// Send the OTP email
				await _emailService.SendEmail(
					to: userLogin.User.Email,
					subject: subject,
					body: body
				);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Failed to send OTP email", error = ex.Message });
			}

			return Ok(new { message = "OTP sent successfully" });
		}

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

		[HttpPost("ResetPassword")]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
		{
			if (string.IsNullOrEmpty(request.Email) ||
				string.IsNullOrEmpty(request.Otp) ||
				string.IsNullOrEmpty(request.NewPassword))
				return BadRequest(new { message = "All fields are required" });

			var userLogin = await _context.UserLogins.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == request.Email);

			if (userLogin == null)
				return NotFound(new { message = "User not found" });

			if (userLogin.Otp != request.Otp || userLogin.OtpExpiry < DateTime.Now)
				return BadRequest(new { message = "Invalid or expired OTP" });

			// Hash new password
			userLogin.PasswordHash = BC.HashPassword(request.NewPassword);
			userLogin.IsDefaultPasswordChanged = true;

			// Clear OTP
			userLogin.Otp = null;
			userLogin.OtpExpiry = null;

			await _context.SaveChangesAsync();

			return Ok(new { message = "Password reset successfully" });
		}


	}
}
