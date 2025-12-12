using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;       // your DBContext namespace
using NatoHrmsBackend.Models;    // your UserDto namespace

namespace NatoHrmsBackend.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public UserController(ApplicationDbContext context)
		{
			_context = context;
		}

		// GET: api/User/All
		[HttpGet("All")]
		public async Task<IActionResult> GetAllUsers()
		{
			var users = await _context.UserLists
				.FromSqlRaw("EXEC Get_All_Users")
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
			{
				return Conflict(new { Message = "Email already exists." });
			}

			// Generate default password and hash it
			string defaultPassword = "Welcome@123"; // You can also generate dynamically if needed
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
				user.OfferLetter, passwordHash // pass the hashed password as last parameter
			);

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
				request.UserName,
				request.IsActive
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
			// convert nullable fields to DBNull
			

			var result = await _context.Database.ExecuteSqlRawAsync(
				"EXEC InsertOrUpdateEmployeeLeave @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9",
				request.LeaveId,                 // @p0
				userName,        // @p1
				request.EmployeeName,    // @p2
				request.FromDate,        // @p3
				request.ToDate,          // @p4
				request.LeaveType,       // @p5
				request.DayType,
				request.Reason,                  // @p6
				request.CancelLeave,
				request.IsApproved      // @p7
			);

			return Ok(new { Message = "Employee leave saved successfully.", RowsAffected = result });
		}

		[HttpGet("GetEmployeeLeave")]
		public async Task<IActionResult> GetEmployeeLeave([FromQuery] string userName = null)
		{
			// Use the query parameter if provided, otherwise use logged-in user
			string targetUser = userName ?? HttpContext.User.Identity.Name;

			var result = await _context.UserLeaveRequests
				.FromSqlRaw("EXEC GetEmployeeLeave @p0", targetUser)
				.ToListAsync();

			return Ok(result);
		}


		[HttpGet("CheckEmail")]
		public async Task<IActionResult> CheckEmail(string email)
		{
			var result = await _context.EmailCheckResponses
				.FromSqlRaw("EXEC CheckEmailExists @p0", email)
				.ToListAsync();

			// Return true/false as boolean
			bool exists = result.Count > 0 && result[0].EmailExists == 1;

			return Ok(new { EmailExists = exists });
		}




	}
}
