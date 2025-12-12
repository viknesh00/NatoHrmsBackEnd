using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Threading.Tasks;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class AttendanceController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public AttendanceController(ApplicationDbContext context)
		{
			_context = context;
		}

		[HttpPost("clock-in")]
		public async Task<IActionResult> ClockIn([FromBody] ClockInRequest req)
		{
			string userEmail = HttpContext.User.Identity.Name;

			// Fix missing clock-out for all users
			await _context.Database.ExecuteSqlRawAsync("EXEC FixMissingClockOutForAll");

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC ClockIn @p0, @p1, @p2",
				userEmail, req.IpAddress, req.Location
			);
			return Ok(new { Message = "Clocked In Successfully" });
		}


		[HttpPost("clock-out")]
		public async Task<IActionResult> ClockOut([FromBody] ClockInRequest req)
		{
			string userEmail = HttpContext.User.Identity.Name;

			await _context.Database.ExecuteSqlRawAsync("EXEC FixMissingClockOutForAll");

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC ClockOut @p0, @p1, @p2",
				userEmail, req.IpAddress, req.Location
				);

			return Ok(new { Message = "Clocked Out Successfully" });
		}

		[HttpPost("GetDailyAttendance")]
		public async Task<IActionResult> GetAttendance([FromBody] AttendanceRequest request)
		{
			string userEmail = HttpContext.User.Identity.Name;

			if (string.IsNullOrEmpty(userEmail))
			{
				return Unauthorized(new { Message = "User email not found in token." });
			}

			var result = await _context.AttendanceResponses
				.FromSqlRaw(
					"EXEC GetAttendanceRecords @p0, @p1",
					userEmail,
					request.Date
				)
				.ToListAsync();

			return Ok(result);
		}

		[HttpPost("GetMonthlyAttendance")]
		public async Task<IActionResult> GetMonthlyAttendance([FromBody] AttendanceMonthlyRequest request)
		{
			string userEmail = HttpContext.User.Identity.Name;

			var sql = "EXEC GetAttendanceRecordsRange @p0, @p1, @p2";

			var result = await _context.AttendanceMonthlyResponses
					.FromSqlRaw(sql, userEmail, request.FromDate, request.ToDate)
					.ToListAsync();

			return Ok(result);

		}

		[HttpGet("GetAllDepartments")]
		public async Task<IActionResult> GetAllDepartments()
		{
			var result = await _context.DepartmentTimings
				.FromSqlRaw("EXEC GetDepartmentTimings")
				.ToListAsync();

			return Ok(result);
		}

		[HttpPost("InsertOrUpdateDepartmentTiming")]
		public async Task<IActionResult> SaveOrUpdateDepartmentTiming([FromBody] DepartmentTiming timing)
		{
			var result = await _context.DepartmentTimings
				.FromSqlRaw(
					"EXEC InsertOrUpdateDepartmentTiming @p0, @p1, @p2, @p3",
					timing.DeptId,
					timing.DepartmentName,
					timing.StartTime,
					timing.EndTime
				)
				.ToListAsync();

			return Ok(new
			{
				message = "Department timing saved successfully",
				data = result.FirstOrDefault()
			});
		}


	}
}
