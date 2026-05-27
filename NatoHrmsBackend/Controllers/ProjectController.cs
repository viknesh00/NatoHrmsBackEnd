using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class ProjectController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public ProjectController(ApplicationDbContext context)
		{
			_context = context;
		}

		// GET api/Project/All
		[HttpGet("All")]
		public async Task<IActionResult> GetAllProjects()
		{
			var result = await _context.ProjectResponses
				.FromSqlRaw("EXEC GetAllProjects")
				.ToListAsync();
			return Ok(result);
		}

		// POST api/Project/InsertOrUpdate
		[HttpPost("InsertOrUpdate")]
		public async Task<IActionResult> InsertOrUpdateProject([FromBody] InsertOrUpdateProjectRequest req)
		{
			if (string.IsNullOrWhiteSpace(req.ProjectName))
				return BadRequest(new { message = "Project name is required." });

			var result = await _context.ProjectResponses
				.FromSqlRaw(
					"EXEC InsertOrUpdateProject @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8",
					req.ProjectId ?? (object)DBNull.Value,
					req.ProjectName,
					req.Description ?? (object)DBNull.Value,
					req.ClientName ?? (object)DBNull.Value,
					req.ManagerEmail ?? (object)DBNull.Value,
					req.StartDate ?? (object)DBNull.Value,
					req.EndDate ?? (object)DBNull.Value,
					req.Status,
					req.Department ?? (object)DBNull.Value
				)
				.ToListAsync();

			var data = result.FirstOrDefault();

			// ── Duplicate detected ──
			if (data?.ProjectId == -1)
				return Conflict(new { message = "A project with this name already exists." });

			bool isNew = req.ProjectId == null || req.ProjectId == 0;
			return Ok(new
			{
				message = isNew ? "Project created successfully." : "Project updated successfully.",
				data
			});
		}
	}
}