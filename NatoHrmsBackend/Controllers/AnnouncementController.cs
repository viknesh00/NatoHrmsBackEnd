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
	public class AnnouncementController : ControllerBase
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmailService _emailService;

		public AnnouncementController(ApplicationDbContext context, IEmailService emailService)
		{
			_context = context;
			_emailService = emailService;
		}

		[HttpPost("SaveAnnouncement")]
		public async Task<IActionResult> SaveAnnouncement([FromBody] Announcement model)
		{
			string userName = HttpContext.User.Identity?.Name ?? "System";
			bool isNew = model.Id == null;

			if (isNew)
			{
				await _context.Database.ExecuteSqlRawAsync(
					@"EXEC AddOrUpdateAnnouncement @p0, @p1, @p2, @p3, @p4, @p5, @p5",
					model.Id, model.AnnouncementDate, model.Description,
					model.Department, model.IsActive, userName
				);
			}
			else
			{
				await _context.Database.ExecuteSqlRawAsync(
					@"EXEC AddOrUpdateAnnouncement @p0, @p1, @p2, @p3, @p4, NULL, @p5",
					model.Id, model.AnnouncementDate, model.Description,
					model.Department, model.IsActive, userName
				);
			}

			// Send email notification to relevant employees for NEW active announcements (non-blocking)
			if (isNew && model.IsActive)
			{
				try
				{
					var allUsers = await _context.Users
						.Where(u => model.Department == "All" || u.Department == model.Department)
						.ToListAsync();

					var announcementDate = model.AnnouncementDate.ToString("dd MMM yyyy") ?? DateTime.Now.ToString("dd MMM yyyy");

					foreach (var emp in allUsers)
					{
						var body = EmailTemplates.AnnouncementEmail(
							$"{emp.FirstName} {emp.LastName}",
							model.Description ?? "",
							model.Department ?? "All",
							announcementDate
						);
						// Fire and forget per recipient
						_ = _emailService.SendEmail(emp.Email, $"📢 New Announcement – {announcementDate} | Natobotics HRMS", body, includeCcFromConfig: true);
					}
				}
				catch { /* Non-blocking */ }
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
