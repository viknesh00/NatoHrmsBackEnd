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
	public class TimeSheetController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public TimeSheetController(ApplicationDbContext context)
		{
			_context = context;
		}

		[HttpPost("InsertOrUpdateTimeSheet")]
		public async Task<IActionResult> InsertOrUpdateTimeSheet([FromBody] List<TimeSheetRequest> requests)
		{
			string username = HttpContext.User.Identity.Name;  // LOGGED-IN USER

			int totalRows = 0; // for response

			foreach (var request in requests)
			{
				var result = await _context.Database.ExecuteSqlRawAsync(
					"EXEC InsertOrUpdateTimesheetEntries @p0,@p1,@p2,@p3,@p4",
					username,                     // @p0
					request.EntryDate,           // @p1
					request.TaskDetails,         // @p2
					request.WorkingHours,        // @p3
					request.LeaveType            // @p4
				);

				totalRows += result; // count rows updated/inserted
			}

			return Ok(new { Message = "Timesheet saved successfully", RowsAffected = totalRows });
		}


		[HttpGet("GetTimeSheet")]
		public async Task<IActionResult> GetTimeSheet(string month)
		{
			string username = HttpContext.User.Identity.Name;

			var data = await _context.TimeSheetResponses
				.FromSqlRaw("EXEC GetTimeSheetEntries @p0, @p1", username, month)
				.ToListAsync();

			return Ok(data);
		}


	}
}
