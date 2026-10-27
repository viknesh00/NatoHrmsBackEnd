using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;
using System.Data;
using System.Text.RegularExpressions;

namespace NatoHrmsBackend.Controllers
{
	// Work status of the employees (SAL code, project manager, period, Active / Deactive)
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class WorkStatusController : ControllerBase
	{
		private static readonly Regex SalRegex = new(@"^[A-Za-z0-9\-_/ ]{1,50}$", RegexOptions.Compiled);

		private readonly ApplicationDbContext _context;

		public WorkStatusController(ApplicationDbContext context)
		{
			_context = context;
		}

		// GET: api/WorkStatus/GetAll?submissionId=5
		[HttpGet("GetAll")]
		public async Task<IActionResult> GetAll([FromQuery] int? submissionId)
		{
			var rows = submissionId.HasValue
				? await _context.WorkStatusRows
					.FromSqlRaw("EXEC GetWorkStatus @SubmissionId = @p0", submissionId.Value)
					.AsNoTracking()
					.ToListAsync()
				: await _context.WorkStatusRows
					.FromSqlRaw("EXEC GetWorkStatus")
					.AsNoTracking()
					.ToListAsync();

			return Ok(rows);
		}

		// GET: api/WorkStatus/Report?from=2026-01-01&to=2026-03-31   (Admin / Manager / Team Lead)
		// A record belongs to the period when it overlaps it: started on / before "to" and not ended before "from".
		// One date only = that single day.
		[HttpGet("Report")]
		[Authorize(Roles = "Admin,Manager,TeamLead")]   // everybody except Employee
		public async Task<IActionResult> Report([FromQuery] DateTime? from, [FromQuery] DateTime? to)
		{
			if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
				return BadRequest(new { message = "The end of the period cannot be before its start." });

			// a single date means that exact day
			if (from.HasValue && !to.HasValue) to = from;
			if (to.HasValue && !from.HasValue) from = to;

			var rows = await _context.WorkStatusRows
				.FromSqlRaw("EXEC GetWorkStatus @FromDate = @FromDate, @ToDate = @ToDate",
					new SqlParameter("@FromDate", SqlDbType.Date) { Value = (object?)from?.Date ?? DBNull.Value },
					new SqlParameter("@ToDate", SqlDbType.Date) { Value = (object?)to?.Date ?? DBNull.Value })
				.AsNoTracking()
				.ToListAsync();

			return Ok(rows);
		}

		// GET: api/WorkStatus/PmList   - people who can be selected as PM (Admin / Manager / Team Lead users)
		[HttpGet("PmList")]
		public async Task<IActionResult> PmList()
		{
			var users = await _context.UserLists
				.FromSqlRaw("EXEC Get_All_Users @p0", HttpContext.User.Identity?.Name ?? "")
				.AsNoTracking()
				.ToListAsync();

			var pms = users
				.Where(u => u.AccessRole == "Admin" || u.AccessRole == "Manager" || u.AccessRole == "TeamLead")
				.Select(u => new { name = $"{u.FirstName} {u.LastName}".Trim(), email = u.Email })
				.Where(u => !string.IsNullOrWhiteSpace(u.name))
				.OrderBy(u => u.name)
				.ToList();

			return Ok(pms);
		}

		// POST: api/WorkStatus/Save   - Write permission creates, Edit permission modifies
		[HttpPost("Save")]
		public async Task<IActionResult> Save([FromBody] WorkStatusRequest model)
		{
			if (model == null) return BadRequest(new { message = "Invalid request." });

			// Employee: view only. Team Lead: can add. Admin / Manager: can add and modify.
			var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
			bool isEdit = model.WorkStatusId.HasValue && model.WorkStatusId.Value > 0;
			bool isAdminOrManager = role == "Admin" || role == "Manager";
			if (isEdit ? !isAdminOrManager : !(isAdminOrManager || role == "TeamLead"))
				return StatusCode(403, new { message = isEdit ? "Only Admin or Manager can modify work status records." : "You are not allowed to add work status records." });

			// ---------- validation ----------
			var errors = new Dictionary<string, string>();

			if (model.SubmissionId <= 0 || !await _context.EmployeeDetailSubmissions.AnyAsync(s => s.SubmissionId == model.SubmissionId && !s.IsDeleted))
				errors["submissionId"] = "Please select a valid employee.";

			var sal = model.SALCode?.Trim();
			if (string.IsNullOrEmpty(sal)) errors["salCode"] = "SAL Code is required.";
			else if (!SalRegex.IsMatch(sal)) errors["salCode"] = "SAL Code contains invalid characters.";

			var pm = model.PMName?.Trim();
			if (string.IsNullOrEmpty(pm)) errors["pmName"] = "PM Name is required.";
			else if (pm.Length > 150 || pm.Any(char.IsControl)) errors["pmName"] = "PM Name is invalid.";

			if (!model.StartDate.HasValue) errors["startDate"] = "Start Date is required.";
			if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value.Date < model.StartDate.Value.Date)
				errors["endDate"] = "End Date cannot be before Start Date.";

			var status = model.WorkStatus?.Trim();
			if (status != "Active" && status != "Deactive") errors["workStatus"] = "Work Status must be Active or Deactive.";

			if (errors.Count > 0)
				return BadRequest(new { message = errors.First().Value, errors });

			if (isEdit)
			{
				var current = await _context.WorkStatusRows
					.FromSqlRaw("EXEC GetWorkStatus @WorkStatusId = @p0", model.WorkStatusId!.Value)
					.AsNoTracking()
					.ToListAsync();
				if (current.Count == 0)
					return NotFound(new { message = "Work status record not found." });
			}

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC SaveWorkStatus @WorkStatusId, @SubmissionId, @SALCode, @PMName, @PMEmail, @StartDate, @EndDate, @WorkStatus, @User",
				new SqlParameter("@WorkStatusId", SqlDbType.Int) { Value = isEdit ? (object)model.WorkStatusId!.Value : DBNull.Value },
				new SqlParameter("@SubmissionId", model.SubmissionId),
				new SqlParameter("@SALCode", sal),
				new SqlParameter("@PMName", pm),
				new SqlParameter("@PMEmail", EmployeeDetailsController.DbOrNull(model.PMEmail)),
				new SqlParameter("@StartDate", SqlDbType.Date) { Value = model.StartDate!.Value.Date },
				new SqlParameter("@EndDate", SqlDbType.Date) { Value = (object?)model.EndDate?.Date ?? DBNull.Value },
				new SqlParameter("@WorkStatus", status),
				new SqlParameter("@User", (object?)User.Identity?.Name ?? DBNull.Value));

			return Ok(new { message = isEdit ? "Work status updated." : "Work status added." });
		}

		// POST: api/WorkStatus/Delete   body: [1,2]   (soft delete - needs Edit permission)
		[HttpPost("Delete")]
		[Authorize(Roles = "Admin,Manager")]
		public async Task<IActionResult> Delete([FromBody] int[] ids)
		{
			if (ids == null || ids.Length == 0)
				return BadRequest(new { message = "No records selected." });

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC DeleteWorkStatus @p0, @p1",
				string.Join(",", ids.Distinct()),
				(object?)User.Identity?.Name ?? DBNull.Value);

			return Ok(new { message = "Deleted successfully." });
		}
	}
}
