using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Security.Claims;

namespace NatoHrmsBackend.Services
{
	// Role based access (Read / Write / Edit) for the Onboarding and Work Status functionality.
	//   Admin and Manager always have full access; Team Lead and Employee follow the RolePermissions table
	//   that Admin / Manager maintain on the Access Control screen.
	//   Usage:  [HrmsPermission("WorkStatus", "Edit")]
	public class HrmsPermissionAttribute : Attribute, IAsyncActionFilter
	{
		public const string Onboarding = "Onboarding";
		public const string WorkStatus = "WorkStatus";

		private readonly string _resource;
		private readonly string _action;   // Read | Write | Edit

		public HrmsPermissionAttribute(string resource, string action)
		{
			_resource = resource;
			_action = action;
		}

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var role = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
			if (IsAdminOrManager(role))
			{
				await next();
				return;
			}

			var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
			var rights = await GetRightsAsync(db, role);
			rights.TryGetValue(_resource, out var r);

			bool allowed = r != null && (_action == "Read" ? r.CanRead : _action == "Write" ? r.CanWrite : r.CanEdit);
			if (!allowed)
			{
				context.Result = new ObjectResult(new { message = $"You do not have {_action} permission for this action." }) { StatusCode = 403 };
				return;
			}

			await next();
		}

		public static bool IsAdminOrManager(string? role) =>
			string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

		// resource -> rights of the given role (a role with no row has no rights)
		public static async Task<Dictionary<string, RolePermissionRow>> GetRightsAsync(ApplicationDbContext db, string? role)
		{
			var rows = await db.RolePermissionRows
				.FromSqlRaw("EXEC GetRolePermissions @p0", role ?? "")
				.AsNoTracking()
				.ToListAsync();

			return rows.ToDictionary(x => x.Resource, x => x, StringComparer.OrdinalIgnoreCase);
		}
	}
}
