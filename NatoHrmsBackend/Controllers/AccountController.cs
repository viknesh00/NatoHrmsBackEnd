using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class AccountController : ControllerBase
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmailService _emailService;

		public AccountController(ApplicationDbContext context, IEmailService emailService)
		{
			_context = context;
			_emailService = emailService;
		}

		// POST: api/Account/ChangePassword
		[HttpPost("ChangePassword")]
		public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
		{
			if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
				return BadRequest(new { Message = "All fields are required." });

			string userName = HttpContext.User.Identity.Name;

			var userLogin = await _context.UserLogins
				.Include(u => u.User)
				.FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);

			if (userLogin == null)
				return NotFound(new { Message = "User not found." });

			if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, userLogin.PasswordHash))
				return BadRequest(new { Message = "Current password is incorrect." });

			if (BCrypt.Net.BCrypt.Verify(request.NewPassword, userLogin.PasswordHash))
				return BadRequest(new { Message = "New password cannot be same as current password." });

			userLogin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
			userLogin.IsDefaultPasswordChanged = true;
			_context.UserLogins.Update(userLogin);
			await _context.SaveChangesAsync();

			// Send confirmation email (non-blocking)
			try
			{
				var body = EmailTemplates.PasswordChangedEmail(userLogin.User.FirstName, userLogin.User.LastName);
				await _emailService.SendEmail(userLogin.User.Email, "Password Changed Successfully – Natobotics HRMS", body);
			}
			catch { /* Non-blocking – don't fail the request if email fails */ }

			return Ok(new { Message = "Password changed successfully." });
		}

		[HttpGet("GetDepartments")]
		public async Task<IActionResult> GetDepartments()
		{
			string userName = HttpContext.User.Identity.Name;
			var result = await _context.DropDownItems
				.FromSqlRaw("EXEC GetDepartments @p0", userName)
				.ToListAsync();
			return Ok(result);
		}

		[HttpGet("GetManagerLists")]
		public async Task<IActionResult> GetNonEmployeeUsers()
		{
			string userName = HttpContext.User.Identity.Name;
			var result = await _context.DropDownItems
				.FromSqlRaw("EXEC GetNonEmployeeUsers @p0", userName)
				.ToListAsync();
			return Ok(result);
		}
	}
}
