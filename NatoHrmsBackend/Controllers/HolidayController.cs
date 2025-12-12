using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class HolidayController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public HolidayController(ApplicationDbContext context)
		{
			_context = context;
		}

		// Get holidays by country
		[HttpGet("GetHolidays")]
		public async Task<IActionResult> GetHolidays()
		{
			string userName = HttpContext.User.Identity.Name;
			var holidays = await _context.HolidayResponses
				.FromSqlRaw("EXEC GetHolidaysByUser @p0", userName)
				.ToListAsync();

			return Ok(holidays);
		}

		// Insert or update holiday
		[HttpPost("SaveHoliday")]
		public async Task<IActionResult> SaveHoliday([FromBody] List<HolidayResponse> models)
		{

			foreach (var model in models)
			{
				await _context.Database.ExecuteSqlRawAsync(
					"EXEC InsertOrUpdateHoliday @p0,@p1,@p2,@p3,@p4",
					model.HolidayId,
					model.EventName,
					model.EventDate,
					model.EventType,
					model.WorkLocation
				);
			}

			return Ok(new { message = "Holidays saved successfully!" });
		}

		[HttpDelete("DeleteHoliday")]
		public async Task<IActionResult> DeleteHoliday([FromQuery] int holidayId)
		{
			await _context.Database.ExecuteSqlRawAsync(
				"EXEC DeleteHoliday @p0",
				holidayId
			);

			return Ok();
		}



	}
}
