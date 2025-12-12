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
	public class AnnouncementController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public AnnouncementController(ApplicationDbContext context)
		{
			_context = context;
		}

		[HttpPost("SaveAnnouncement")]
		public async Task<IActionResult> SaveAnnouncement([FromBody] Announcement model)
		{
			string userName = HttpContext.User.Identity?.Name ?? "System";

			if (model.Id == null)
			{
				// INSERT (Id = null)
				await _context.Database.ExecuteSqlRawAsync(
					@"EXEC AddOrUpdateAnnouncement 
                @p0, @p1, @p2, @p3, @p4, @p5, @p5",
					model.Id,                // @p0 → NULL
					model.AnnouncementDate,  // @p1
					model.Description,       // @p2
					model.Department,        // @p3
					model.IsActive,          // @p4
					userName                 // @p5 → CreatedBy & UpdatedBy
				);
			}
			else
			{
				// UPDATE
				await _context.Database.ExecuteSqlRawAsync(
					@"EXEC AddOrUpdateAnnouncement 
                @p0, @p1, @p2, @p3, @p4, NULL, @p5",
					model.Id,                // @p0 → existing Id
					model.AnnouncementDate,  // @p1
					model.Description,       // @p2
					model.Department,        // @p3
					model.IsActive,          // @p4
					userName                 // @p5 → UpdatedBy
				);
			}

			return Ok(new { Message = "Announcement saved successfully" });
		}


		[HttpGet("GetAnnouncement")]
		public async Task<IActionResult> GetAllAnnouncements()
		{
			string userName = HttpContext.User.Identity.Name;
			var announcements = await _context.AnnouncementLists
				.FromSqlRaw("EXEC GetAnnouncements @p0", userName)
				.ToListAsync();

			return Ok(announcements);
		}



	}
}
