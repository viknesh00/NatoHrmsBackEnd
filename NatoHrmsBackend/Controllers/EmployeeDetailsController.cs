using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NatoHrmsBackend.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class EmployeeDetailsController : ControllerBase
	{
		private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB per file

		private readonly ApplicationDbContext _context;
		private readonly IEmailService _emailService;
		private readonly IConfiguration _config;
		private readonly ILogger<EmailService> _logger;

		public EmployeeDetailsController(ApplicationDbContext context, IConfiguration config, IEmailService emailService, ILogger<EmailService> logger)
		{
			_context = context;
			_config = config;
			_emailService = emailService;
			_logger = logger;
		}

		// Public endpoint - the employee details form is shared by link, no login required
		[AllowAnonymous]
		[HttpPost("Submit")]
		[RequestSizeLimit(12 * 1024 * 1024)]
		public async Task<IActionResult> Submit([FromForm] EmployeeDetailsRequest model)
		{
			if (model == null)
				return BadRequest(new { message = "Invalid request." });

			// ---------- Server-side validation ----------
			var errors = Validate(model, out var dob);
			if (errors.Count > 0)
				return BadRequest(new { message = errors.First().Value, errors });

			// ---------- Files: both are mandatory ----------
			var fileErrors = new Dictionary<string, string>();
			var passportCopy = await ReadFileAsync(model.PassportCopy, "Passport copy", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, fileErrors, "passportCopy");
			var passportPhoto = await ReadFileAsync(model.PassportPhoto, "Passport-size photo", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, fileErrors, "passportPhoto");
			if (fileErrors.Count > 0)
				return BadRequest(new { message = fileErrors.First().Value, errors = fileErrors });

			var firstName = model.FirstName.Trim();
			var lastName = model.LastName.Trim();
			var passportNo = model.PassportNo.Trim().ToUpperInvariant();

			// Duplicate submission check (same passport number)
			bool alreadySubmitted = await _context.EmployeeDetailSubmissions
				.AnyAsync(s => s.PassportNo == passportNo && !s.IsDeleted);

			if (alreadySubmitted)
				return Conflict(new { message = "Details for this passport number have already been submitted. Please contact HR if you need to make a correction." });

			try
			{
				var submissionId = new SqlParameter("@SubmissionId", SqlDbType.Int) { Direction = ParameterDirection.Output };

				var parameters = new[]
				{
					new SqlParameter("@FirstName", firstName),
					new SqlParameter("@LastName", lastName),
					new SqlParameter("@DateOfBirth", SqlDbType.Date) { Value = dob },
					new SqlParameter("@PlaceOfBirth", model.PlaceOfBirth.Trim()),
					new SqlParameter("@MobileNumber", model.MobileNumber.Trim()),
					new SqlParameter("@PassportNo", passportNo),
					new SqlParameter("@SALCode", EmployeeDetailsController.DbOrNull(model.SALCode)),
					new SqlParameter("@City", model.City.Trim()),
					new SqlParameter("@Country", model.Country.Trim()),
					new SqlParameter("@CurrentAddress", model.CurrentAddress.Trim()),
					new SqlParameter("@NConsEmail", EmployeeDetailsController.DbOrNull(model.NConsEmail)),
					new SqlParameter("@NConsEmailStatus", EmployeeDetailsController.DbOrNull(model.NConsEmailStatus)),
					new SqlParameter("@PersonalEmail", model.PersonalEmail.Trim()),
					new SqlParameter("@VisaType", EmployeeDetailsController.DbOrNull(model.VisaType)),
					new SqlParameter("@WorkVisaExpiryDate", SqlDbType.Date) { Value = (object?)EmployeeDetailsController.ParseDmy(model.WorkVisaExpiryDate) ?? DBNull.Value },
					new SqlParameter("@HasSiemensIdCard", EmployeeDetailsController.DbOrNull(model.HasSiemensIdCard)),
					new SqlParameter("@WhatsAppNumber", EmployeeDetailsController.DbOrNull(model.WhatsAppNumber)),
					new SqlParameter("@SubmittedFromIp", (object?)HttpContext.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value),
					new SqlParameter("@PassportCopyFileName", passportCopy.FileName),
					new SqlParameter("@PassportCopyContentType", passportCopy.ContentType),
					new SqlParameter("@PassportCopyData", SqlDbType.VarBinary) { Value = passportCopy.Content },
					new SqlParameter("@PassportPhotoFileName", passportPhoto.FileName),
					new SqlParameter("@PassportPhotoContentType", passportPhoto.ContentType),
					new SqlParameter("@PassportPhotoData", SqlDbType.VarBinary) { Value = passportPhoto.Content },
					submissionId
				};

				await _context.Database.ExecuteSqlRawAsync(
					@"EXEC InsertEmployeeDetails
					@FirstName, @LastName, @DateOfBirth, @PlaceOfBirth, @MobileNumber, @PassportNo, @SALCode,
					@City, @Country, @CurrentAddress, @NConsEmail, @NConsEmailStatus, @PersonalEmail, @VisaType, @WorkVisaExpiryDate, @HasSiemensIdCard, @WhatsAppNumber, @SubmittedFromIp,
					@PassportCopyFileName, @PassportCopyContentType, @PassportCopyData,
					@PassportPhotoFileName, @PassportPhotoContentType, @PassportPhotoData,
					@SubmissionId OUTPUT",
					parameters
				);

				int id = submissionId.Value is int v ? v : 0;

				// Notify HR (non-blocking - the submission is already saved)
				try
				{
					var toEmail = _config["EmployeeDetailsForm:ToEmail"];
					if (!string.IsNullOrWhiteSpace(toEmail))
					{
						var body = EmailTemplates.EmployeeDetailsSubmittedEmail(
							id, firstName, lastName, dob.ToString("dd/MM/yyyy"), model.PlaceOfBirth.Trim(),
							model.MobileNumber.Trim(), passportNo, OrDash(model.SALCode), model.City.Trim(),
							model.Country.Trim(), model.CurrentAddress.Trim(), OrDash(model.NConsEmail),
							OrDash(model.NConsEmailStatus), model.PersonalEmail.Trim(),
							OrDash(model.VisaType), ParseDmy(model.WorkVisaExpiryDate)?.ToString("dd/MM/yyyy") ?? "-",
							OrDash(model.HasSiemensIdCard), OrDash(model.WhatsAppNumber),
							DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

						await _emailService.SendEmail(
							to: toEmail,
							subject: $"Employee Details Received – {firstName} {lastName}",
							body: body,
							cc: _config["EmployeeDetailsForm:CcEmails"],
							attachments: new[]
							{
								new EmailAttachment { FileName = passportCopy.FileName, ContentType = passportCopy.ContentType, Content = passportCopy.Content },
								new EmailAttachment { FileName = passportPhoto.FileName, ContentType = passportPhoto.ContentType, Content = passportPhoto.Content }
							}
						);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to send employee details notification email");
				}

				return Ok(new { message = "Employee details submitted successfully.", submissionId = id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to save employee details");
				return StatusCode(500, new { message = "We could not save your details. Please try again later." });
			}
		}

		// ---------------------------------------------------------------------
		// Helpers
		// ---------------------------------------------------------------------
		private static readonly Regex NameRegex = new(@"^[\p{L}\p{M}][\p{L}\p{M} '.\-]*$", RegexOptions.Compiled);
		private static readonly Regex PassportRegex = new(@"^[A-Za-z0-9]{6,20}$", RegexOptions.Compiled);
		private static readonly Regex SalRegex = new(@"^[A-Za-z0-9\-_/ ]{1,50}$", RegexOptions.Compiled);
		private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

		private static bool HasControlChars(string v, bool allowNewLine = false) =>
			v.Any(c => char.IsControl(c) && !(allowNewLine && (c == '\n' || c == '\r')));

		internal static Dictionary<string, string> Validate(EmployeeDetailsRequest m, out DateTime dob)
		{
			var e = new Dictionary<string, string>();
			dob = default;

			void Required(string key, string label, string value, int max, bool allowNewLine = false)
			{
				var v = value?.Trim();
				if (string.IsNullOrEmpty(v)) { e[key] = $"{label} is required."; return; }
				if (v.Length > max) { e[key] = $"{label} must not exceed {max} characters."; return; }
				if (HasControlChars(v, allowNewLine)) e[key] = $"{label} contains invalid characters.";
			}

			Required("firstName", "First Name", m.FirstName, 100);
			if (!e.ContainsKey("firstName") && !NameRegex.IsMatch(m.FirstName.Trim())) e["firstName"] = "First Name contains invalid characters.";

			Required("lastName", "Last Name", m.LastName, 100);
			if (!e.ContainsKey("lastName") && !NameRegex.IsMatch(m.LastName.Trim())) e["lastName"] = "Last Name contains invalid characters.";

			// Date of birth - DD/MM/YYYY
			if (string.IsNullOrWhiteSpace(m.DateOfBirth))
				e["dateOfBirth"] = "Date of Birth is required.";
			else if (!DateTime.TryParseExact(m.DateOfBirth.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
				e["dateOfBirth"] = "Date of Birth must be in DD/MM/YYYY format.";
			else if (dob.Date > DateTime.Today || dob.Year < 1900)
				e["dateOfBirth"] = "Please enter a valid Date of Birth.";

			Required("placeOfBirth", "Place of Birth", m.PlaceOfBirth, 150);

			// Mobile number with country code, e.g. +91 98765 43210
			Required("mobileNumber", "Mobile Number", m.MobileNumber, 25);
			if (!e.ContainsKey("mobileNumber"))
			{
				var raw = m.MobileNumber.Trim();
				var digits = new string(raw.Where(char.IsDigit).ToArray());
				if (!raw.StartsWith("+") || !Regex.IsMatch(raw, @"^\+[0-9][0-9 \-]*$") || digits.Length < 8 || digits.Length > 15)
					e["mobileNumber"] = "Mobile Number must include the country code, e.g. +91 98765 43210.";
			}

			Required("passportNo", "Passport No.", m.PassportNo, 20);
			if (!e.ContainsKey("passportNo") && !PassportRegex.IsMatch(m.PassportNo.Trim()))
				e["passportNo"] = "Passport No. must be 6-20 letters or digits.";

			// SAL Code is optional
			if (!string.IsNullOrWhiteSpace(m.SALCode) && !SalRegex.IsMatch(m.SALCode.Trim()))
				e["salCode"] = "SAL Code contains invalid characters.";

			Required("city", "City", m.City, 100);
			Required("country", "Country", m.Country, 100);
			Required("currentAddress", "Current Address", m.CurrentAddress, 500, allowNewLine: true);

			// N-Cons Email ID and N-Cons Email Status are optional
			if (!string.IsNullOrWhiteSpace(m.NConsEmail)
				&& (m.NConsEmail.Trim().Length > 150 || !EmailRegex.IsMatch(m.NConsEmail.Trim())))
				e["nConsEmail"] = "Please enter a valid N-Cons Email ID.";

			var status = m.NConsEmailStatus?.Trim();
			if (!string.IsNullOrEmpty(status) && status != "Enabled" && status != "Disabled")
				e["nConsEmailStatus"] = "N-Cons Email Status must be Enabled or Disabled.";

			Required("personalEmail", "Personal Email ID", m.PersonalEmail, 150);
			if (!e.ContainsKey("personalEmail") && !EmailRegex.IsMatch(m.PersonalEmail.Trim()))
				e["personalEmail"] = "Please enter a valid Personal Email ID.";

			// Work authorisation / contact - all optional
			if (!string.IsNullOrWhiteSpace(m.VisaType)
				&& (m.VisaType.Trim().Length > 100 || HasControlChars(m.VisaType.Trim())))
				e["visaType"] = "Visa / Work Permit Type is invalid or longer than 100 characters.";

			if (!string.IsNullOrWhiteSpace(m.WorkVisaExpiryDate) && ParseDmy(m.WorkVisaExpiryDate) == null)
				e["workVisaExpiryDate"] = "Work Visa Expiry Date must be a valid date in DD/MM/YYYY format.";

			var siemens = m.HasSiemensIdCard?.Trim();
			if (!string.IsNullOrEmpty(siemens) && siemens != "Yes" && siemens != "No")
				e["hasSiemensIdCard"] = "Siemens ID Card must be Yes or No.";

			if (!string.IsNullOrWhiteSpace(m.WhatsAppNumber))
			{
				var raw = m.WhatsAppNumber.Trim();
				var digits = new string(raw.Where(char.IsDigit).ToArray());
				if (!raw.StartsWith("+") || !Regex.IsMatch(raw, @"^\+[0-9][0-9 \-]*$") || digits.Length < 8 || digits.Length > 15)
					e["whatsAppNumber"] = "WhatsApp Number must include the country code, e.g. +91 98765 43210.";
			}

			return e;
		}

		// DD/MM/YYYY -> date (null when blank / invalid)
		internal static DateTime? ParseDmy(string? value)
		{
			if (string.IsNullOrWhiteSpace(value)) return null;
			if (!DateTime.TryParseExact(value.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
				return null;
			return d.Year < 1900 || d.Year > 2100 ? null : d;
		}

		// Optional text -> NULL in the database when left blank
		internal static object DbOrNull(string? value) =>
			string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();

		// Optional text -> "-" in the email when left blank
		internal static string OrDash(string? value) =>
			string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

		internal sealed class UploadedFile
		{
			public string FileName { get; set; } = "";
			public string ContentType { get; set; } = "";
			public byte[] Content { get; set; } = Array.Empty<byte>();
		}

		// Validates extension, size and real file signature, then returns the bytes.
		// The stored content type is derived from the file signature, not from what the client claims.
		internal static async Task<UploadedFile> ReadFileAsync(IFormFile file, string label, string[] allowedExtensions, Dictionary<string, string> errors, string key)
		{
			if (file == null || file.Length == 0)
			{
				errors[key] = $"{label} is required.";
				return null;
			}
			if (file.Length > MaxFileBytes)
			{
				errors[key] = $"{label} must not exceed 5 MB.";
				return null;
			}

			var ext = Path.GetExtension(Path.GetFileName(file.FileName ?? "")).ToLowerInvariant();
			if (!allowedExtensions.Contains(ext))
			{
				errors[key] = $"{label} must be a {string.Join(", ", allowedExtensions.Select(x => x.TrimStart('.').ToUpperInvariant()))} file.";
				return null;
			}

			byte[] bytes;
			using (var ms = new MemoryStream())
			{
				await file.CopyToAsync(ms);
				bytes = ms.ToArray();
			}

			string detected = null;
			if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) detected = "image/jpeg";
			else if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) detected = "image/png";
			else if (bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D) detected = "application/pdf";

			var allowedTypes = allowedExtensions.Select(x => x == ".pdf" ? "application/pdf" : x == ".png" ? "image/png" : "image/jpeg");
			if (detected == null || !allowedTypes.Contains(detected))
			{
				errors[key] = $"{label} is not a valid file.";
				return null;
			}

			// Keep a safe display name only (no path, no unusual characters)
			var baseName = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName ?? ""), @"[^\w\.\- ()]", "_");
			if (string.IsNullOrWhiteSpace(baseName)) baseName = key;
			if (baseName.Length > 200) baseName = baseName.Substring(0, 200);

			return new UploadedFile { FileName = baseName + ext, ContentType = detected, Content = bytes };
		}
	}
}
