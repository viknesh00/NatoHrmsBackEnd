using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;
using System.Security.Claims;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class PermissionsController : ControllerBase
	{
		// Roles whose rights Admin / Manager can change (Admin and Manager always have full access)
		private static readonly string[] ManagedRoles = { "TeamLead", "Employee" };
		private static readonly string[] Resources = { HrmsPermissionAttribute.Onboarding, HrmsPermissionAttribute.WorkStatus };

		private readonly ApplicationDbContext _context;

		public PermissionsController(ApplicationDbContext context)
		{
			_context = context;
		}

		private string? CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value;

		// GET: api/Permissions/Mine  - what the logged in user may do (used by the UI to show / hide actions)
		[HttpGet("Mine")]
		public async Task<IActionResult> Mine()
		{
			bool full = HrmsPermissionAttribute.IsAdminOrManager(CurrentRole);
			var rights = full ? new Dictionary<string, RolePermissionRow>() : await HrmsPermissionAttribute.GetRightsAsync(_context, CurrentRole);

			object Of(string resource)
			{
				if (full) return new { read = true, write = true, edit = true };
				rights.TryGetValue(resource, out var r);
				return new { read = r?.CanRead ?? false, write = r?.CanWrite ?? false, edit = r?.CanEdit ?? false };
			}

			return Ok(new
			{
				role = CurrentRole,
				isAdminOrManager = full,
				onboarding = Of(HrmsPermissionAttribute.Onboarding),
				workStatus = Of(HrmsPermissionAttribute.WorkStatus)
			});
		}

		// GET: api/Permissions/All  - matrix for the Access Control screen (Admin / Manager only)
		[HttpGet("All")]
		public async Task<IActionResult> All()
		{
			if (!HrmsPermissionAttribute.IsAdminOrManager(CurrentRole))
				return StatusCode(403, new { message = "Only Admin or Manager can manage permissions." });

			var rows = await _context.RolePermissionRows
				.FromSqlRaw("EXEC GetRolePermissions")
				.AsNoTracking()
				.ToListAsync();

			// one entry per managed role + resource, even when nothing is stored yet
			var result = ManagedRoles.SelectMany(role => Resources.Select(resource =>
			{
				var r = rows.FirstOrDefault(x => x.Role == role && x.Resource == resource);
				return new { role, resource, canRead = r?.CanRead ?? false, canWrite = r?.CanWrite ?? false, canEdit = r?.CanEdit ?? false };
			}));

			return Ok(result);
		}

		// POST: api/Permissions/Save   body: [{ role, resource, canRead, canWrite, canEdit }]
		[HttpPost("Save")]
		public async Task<IActionResult> Save([FromBody] List<RolePermissionRow> items)
		{
			if (!HrmsPermissionAttribute.IsAdminOrManager(CurrentRole))
				return StatusCode(403, new { message = "Only Admin or Manager can manage permissions." });

			if (items == null || items.Count == 0)
				return BadRequest(new { message = "Nothing to save." });

			foreach (var i in items)
			{
				if (!ManagedRoles.Contains(i.Role) || !Resources.Contains(i.Resource))
					return BadRequest(new { message = "Invalid role or functionality." });
			}

			foreach (var i in items)
			{
				// Write and Edit imply Read - a role that can add or modify must also be able to see
				bool canRead = i.CanRead || i.CanWrite || i.CanEdit;
				await _context.Database.ExecuteSqlRawAsync(
					"EXEC SaveRolePermission @Role, @Resource, @CanRead, @CanWrite, @CanEdit, @UpdatedBy",
					new SqlParameter("@Role", i.Role),
					new SqlParameter("@Resource", i.Resource),
					new SqlParameter("@CanRead", canRead),
					new SqlParameter("@CanWrite", i.CanWrite),
					new SqlParameter("@CanEdit", i.CanEdit),
					new SqlParameter("@UpdatedBy", (object?)User.Identity?.Name ?? DBNull.Value));
			}

			return Ok(new { message = "Permissions saved." });
		}
	}
}
