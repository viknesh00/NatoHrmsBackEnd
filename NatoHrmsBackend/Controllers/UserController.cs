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
	public class UserController : ControllerBase
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmailService _emailService;

		public UserController(ApplicationDbContext context, IEmailService emailService)
		{
			_context = context;
			_emailService = emailService;
		}

		// GET: api/User/All
		[HttpGet("All")]
		public async Task<IActionResult> GetAllUsers()
		{
			string userName = HttpContext.User.Identity.Name;
			var users = await _context.UserLists
				.FromSqlRaw("EXEC Get_All_Users @p0", userName)
				.ToListAsync();
			return Ok(users);
		}

		[HttpGet("GetUser/{UserID}")]
		public IActionResult GetUserByEmail(string UserID)
		{
			var result = _context.Users
				.FromSqlRaw("EXEC GetUser @p0", UserID)
				.ToList();
			return Ok(result);
		}

		[HttpPost("Add")]
		public async Task<IActionResult> AddUser([FromBody] User user)
		{
			var emailExists = await _context.Users.AnyAsync(u => u.Email == user.Email);
			if (emailExists)
				return Conflict(new { Message = "Email already exists." });

			string defaultPassword = "Welcome@123";
			string passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC AddUser 
                @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,@p18,@p19,@p20,
                @p21,@p22,@p23,@p24,@p25,@p26,@p27,@p28,@p29,@p30,@p31,@p32,@p33,@p34,@p35,@p36,@p37,@p38,@p39,@p40,
                @p41,@p42,@p43,@p44,@p45,@p46,@p47,@p48,@p49,@p50",
				user.FirstName, user.LastName, user.Gender, user.DOB, user.MaritalStatus,
				user.Nationality, user.BloodGroup, user.ContactNumber, user.Email, user.Address,
				user.EmployeeType, user.Department, user.Designation, user.DOJ, user.WorkLocation,
				user.ReportingManager, user.AccessRole, user.EmploymentStatus, user.EmployeeId,
				user.CTC, user.BasicSalary, user.HRA, user.EmployeePF, user.PFAccountNumber,
				user.MedicalAllowance, user.ConveyanceAllowance, user.ESINumber, user.SpecialAllowance,
				user.BankName, user.AccountNumber, user.IFSCCode, user.PanNumber, user.UANNumber,
				user.HighestQualification, user.Specialization, user.University, user.YearOfPassing,
				user.PreviousCompany, user.TotalExperience, user.EmergencyContactName,
				user.EmergencyContactNumber, user.Relationship, user.WorkShift,
				user.WorkMode, user.Notes, user.ProfilePhoto, user.Resume, user.AadharCard, user.PanCard,
				user.OfferLetter, passwordHash
			);

			// Send welcome email with credentials (non-blocking)
			try
			{
				var body = EmailTemplates.WelcomeEmail(
					user.FirstName, user.LastName, user.Email, defaultPassword, user.EmployeeId ?? "N/A");
				await _emailService.SendEmail(user.Email, "Welcome to Natobotics HRMS – Your Account is Ready 🎉", body);
			}
			catch { /* Non-blocking */ }

			return Ok(new { Message = "User Created Successfully", DefaultPassword = defaultPassword });
		}

		[HttpPost("Edit")]
		public async Task<IActionResult> EditUser([FromBody] User user)
		{
			await _context.Database
				.ExecuteSqlRawAsync("EXEC UpdateUser @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,@p18,@p19,@p20,@p21,@p22,@p23,@p24,@p25,@p26,@p27,@p28,@p29,@p30,@p31,@p32,@p33,@p34,@p35,@p36,@p37,@p38,@p39,@p40,@p41,@p42,@p43,@p44,@p45,@p46,@p47,@p48,@p49",
					user.Email, user.FirstName, user.LastName, user.Gender, user.DOB,
					user.MaritalStatus, user.Nationality, user.BloodGroup, user.ContactNumber,
					user.Address, user.EmployeeType, user.Department, user.Designation,
					user.DOJ, user.WorkLocation, user.ReportingManager, user.AccessRole,
					user.EmploymentStatus, user.EmployeeId, user.CTC, user.BasicSalary,
					user.HRA, user.EmployeePF, user.PFAccountNumber, user.MedicalAllowance,
					user.ConveyanceAllowance, user.ESINumber, user.SpecialAllowance,
					user.BankName, user.AccountNumber, user.IFSCCode, user.PanNumber,
					user.UANNumber, user.HighestQualification, user.Specialization,
					user.University, user.YearOfPassing, user.PreviousCompany,
					user.TotalExperience, user.EmergencyContactName, user.EmergencyContactNumber,
					user.Relationship, user.WorkShift, user.WorkMode, user.Notes, user.ProfilePhoto,
					user.Resume, user.AadharCard, user.PanCard, user.OfferLetter
				);
			return Ok(new { Message = "User Updated Successfully" });
		}

		[HttpPost("UpdateUserStaus")]
		public async Task<IActionResult> UpdateIsActive([FromBody] StatusUpdateRequest request)
		{
			if (string.IsNullOrEmpty(request.UserName))
				return BadRequest(new { Message = "UserName is required." });

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC UpdateUserStaus @p0, @p1",
				request.UserName, request.IsActive
			);
			return Ok(new { Message = "Status updated successfully." });
		}

		[HttpGet("GetSalary")]
		public async Task<IActionResult> GetSalary()
		{
			var result = await _context.SalaryDetails
				.FromSqlRaw("EXEC GetSalaryDetails")
				.ToListAsync();
			return Ok(result);
		}

		[HttpPost("ApplyLeave")]
		public async Task<IActionResult> InsertOrUpdateEmployeeLeave([FromBody] UserLeaveRequest request)
		{
			string userName = HttpContext.User.Identity.Name;

			var result = await _context.Database.ExecuteSqlRawAsync(
				"EXEC InsertOrUpdateEmployeeLeave @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9",
				request.LeaveId, userName, request.EmployeeName, request.FromDate,
				request.ToDate, request.LeaveType, request.DayType, request.Reason,
				request.CancelLeave, request.IsApproved
			);

			// Notify manager/admin about new leave request (non-blocking)
			try
			{
				// Find the reporting manager's email
				var userInfo = await _context.Users.FirstOrDefaultAsync(u => u.Email == userName);
				if (userInfo?.ReportingManager != null)
				{
					var managerInfo = await _context.Users.FirstOrDefaultAsync(u => u.Email == userInfo.ReportingManager);
					if (managerInfo != null)
					{
						var body = EmailTemplates.LeaveRequestNotificationEmail(
							$"{managerInfo.FirstName} {managerInfo.LastName}",
							request.EmployeeName ?? userName,
							request.LeaveType ?? "Leave",
							request.FromDate.ToString("dd MMM yyyy") ?? "",
							request.ToDate.ToString("dd MMM yyyy") ?? "",
							request.Reason ?? "Not specified"
						);
						await _emailService.SendEmail(
							managerInfo.Email,
							$"Leave Request from {request.EmployeeName} – Action Required",
							body,
							cc: userName,
							includeCcFromConfig: true
						);
					}
				}
			}
			catch { /* Non-blocking */ }
			return Ok(new { Message = "Employee leave saved successfully.", RowsAffected = result });
		}

		[HttpGet("GetEmployeeLeave")]
		public async Task<IActionResult> GetEmployeeLeave([FromQuery] string userName = null)
		{
			string targetUser = userName ?? HttpContext.User.Identity.Name;

			var result = await _context.UserLeaveRequests
				.FromSqlRaw("EXEC GetEmployeeLeave @p0", targetUser)
				.ToListAsync();

			var holidays = await _context.HolidayResponses
				.FromSqlRaw("EXEC GetHolidaysByUser @p0", targetUser)
				.ToListAsync();

			return Ok(new { Leaves = result, Holidays = holidays });
		}

		[HttpPost("ApproveRejectLeave")]
		public async Task<IActionResult> ApproveOrRejectLeave([FromBody] ApproveLeaveRequest request)
		{
			string approver = HttpContext.User.Identity.Name;

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC ApproveOrRejectLeave @p0, @p1, @p2, @p3",
				request.LeaveId, request.IsApproved, approver, request.ApproverReason
			);

			// Send approval/decline email to employee (non-blocking)
			try
			{
				// Fetch leave + employee details via GetEmployeeLeave filtered by approver's view
				var leaveRecord = await _context.UserLeaveRequests
					.FromSqlRaw("EXEC GetLeaveById @p0", request.LeaveId)
					.ToListAsync();

				if (leaveRecord.Count > 0)
				{
					var leave = leaveRecord[0];
					var empEmail = leave.UserName;

					var empUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == empEmail);
					if (empUser != null)
					{
						var empName = $"{empUser.FirstName} {empUser.LastName}";
						var body = EmailTemplates.LeaveApprovalEmail(
							empName,
							leave.LeaveType ?? "Leave",
							leave.FromDate.ToString("dd MMM yyyy"),
							leave.ToDate.ToString("dd MMM yyyy"),
							request.IsApproved,
							request.ApproverReason ?? "",
							approver
						);
						var subject = request.IsApproved
							? "Your Leave Request Has Been Approved ✅ – Natobotics HRMS"
							: "Your Leave Request Has Been Declined ❌ – Natobotics HRMS";

						await _emailService.SendEmail(
							empEmail, subject, body,
							includeCcFromConfig: true
						);
					}
				}
			}
			catch { /* Non-blocking */ }
			return Ok(new { Message = "Leave processed successfully." });
		}

		[HttpGet("CheckEmail")]
		public async Task<IActionResult> CheckEmail(string email)
		{
			var result = await _context.EmailCheckResponses
				.FromSqlRaw("EXEC CheckEmailExists @p0", email)
				.ToListAsync();
			bool exists = result.Count > 0 && result[0].EmailExists == 1;
			return Ok(new { EmailExists = exists });
		}
	}
}
