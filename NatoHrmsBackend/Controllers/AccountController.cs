using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Threading.Tasks;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class AccountController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public AccountController(ApplicationDbContext context)
		{
			_context = context;
		}

		// POST: api/User/ChangePassword
		[HttpPost("ChangePassword")]
		public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
		{
			if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
				return BadRequest(new { Message = "All fields are required." });

			// Get logged-in username (email/employeeId)
			string userName = HttpContext.User.Identity.Name;

			// Fetch user login record
			var userLogin = await _context.UserLogins
				.FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);

			if (userLogin == null)
				return NotFound(new { Message = "User not found." });

			// Verify current password
			bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, userLogin.PasswordHash);
			if (!isCurrentPasswordValid)
				return BadRequest(new { Message = "Current password is incorrect." });

			// Check if new password is same as old password
			if (BCrypt.Net.BCrypt.Verify(request.NewPassword, userLogin.PasswordHash))
				return BadRequest(new { Message = "New password cannot be same as current password." });

			// Hash new password
			string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

			// Update password in UserLogins table
			userLogin.PasswordHash = newHashedPassword;
			userLogin.IsDefaultPasswordChanged = true;

			_context.UserLogins.Update(userLogin);
			await _context.SaveChangesAsync();

			return Ok(new { Message = "Password changed successfully." });
		}

		[HttpGet("GetDepartments")]
		public async Task<IActionResult> GetDepartments()
		{
			// Call the stored procedure
			var result = await _context.DropDownItems
				.FromSqlRaw("EXEC GetDepartments")
				.ToListAsync(); // no projection needed


			return Ok(result);
		}

		[HttpGet("GetManagerLists")]
		public async Task<IActionResult> GetNonEmployeeUsers()
		{
			var result = await _context.DropDownItems
				.FromSqlRaw("EXEC GetNonEmployeeUsers")
				.ToListAsync();

			return Ok(result);
		}


	}
}
