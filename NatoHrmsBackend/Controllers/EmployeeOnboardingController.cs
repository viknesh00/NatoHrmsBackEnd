using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;
using System.Data;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NatoHrmsBackend.Controllers
{
	// HRMS screen for the submissions received from the public employee details form.
	// Menu visibility (department / role) is managed in the frontend Sidebar, like the other screens.
	[Authorize]
	[HrmsPermission(HrmsPermissionAttribute.Onboarding, "Read")]   // every action needs Read; modifying actions also need Edit
	[ApiController]
	[Route("api/[controller]")]
	public class EmployeeOnboardingController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public EmployeeOnboardingController(ApplicationDbContext context)
		{
			_context = context;
		}

		// GET: api/EmployeeOnboarding/GetAll
		[HttpGet("GetAll")]
		public async Task<IActionResult> GetAll()
		{
			var result = await _context.EmployeeDetailLists
				.FromSqlRaw("EXEC GetEmployeeDetails")
				.AsNoTracking()
				.ToListAsync();

			return Ok(result);
		}

		// GET: api/EmployeeOnboarding/Get/5
		[HttpGet("Get/{id}")]
		public async Task<IActionResult> Get(int id)
		{
			var rows = await _context.EmployeeDetailLists
				.FromSqlRaw("EXEC GetEmployeeDetails @p0", id)
				.AsNoTracking()
				.ToListAsync();

			var row = rows.FirstOrDefault();
			if (row == null) return NotFound(new { message = "Submission not found." });
			return Ok(row);
		}

		// GET: api/EmployeeOnboarding/File/5/PassportCopy  (opens / downloads one attachment)
		[HttpGet("File/{id}/{fileType}")]
		public async Task<IActionResult> GetFile(int id, string fileType)
		{
			if (fileType != "PassportCopy" && fileType != "PassportPhoto")
				return BadRequest(new { message = "Invalid file type." });

			var files = await _context.EmployeeDetailFileResults
				.FromSqlRaw("EXEC GetEmployeeDetailFiles @p0", id.ToString())
				.AsNoTracking()
				.ToListAsync();

			var file = files.FirstOrDefault(f => f.FileType == fileType);
			if (file == null || file.FileData == null || file.FileData.Length == 0)
				return NotFound(new { message = "File not found." });

			return File(file.FileData, file.ContentType, file.FileName);
		}

		// GET: api/EmployeeOnboarding/Certificates/5   (list of certificates of one submission, no file bytes)
		[HttpGet("Certificates/{id}")]
		public async Task<IActionResult> GetCertificates(int id)
		{
			var list = await _context.EmployeeCertificateResults
				.FromSqlRaw("EXEC GetEmployeeCertificates @SubmissionIds = @p0, @IncludeData = @p1", id.ToString(), false)
				.AsNoTracking()
				.ToListAsync();

			return Ok(list.Select(c => new { c.CertificateId, c.SubmissionId, c.FileName, c.ContentType, c.FileSize }));
		}

		// GET: api/EmployeeOnboarding/Certificate/12   (opens / downloads one certificate)
		[HttpGet("Certificate/{certificateId}")]
		public async Task<IActionResult> GetCertificate(int certificateId)
		{
			var list = await _context.EmployeeCertificateResults
				.FromSqlRaw("EXEC GetEmployeeCertificates @CertificateId = @p0", certificateId)
				.AsNoTracking()
				.ToListAsync();

			var cert = list.FirstOrDefault();
			if (cert == null || cert.FileData == null || cert.FileData.Length == 0)
				return NotFound(new { message = "File not found." });

			return File(cert.FileData, cert.ContentType, cert.FileName);
		}

		// POST: api/EmployeeOnboarding/Update  (details + optional replacement files + certificates to add / remove)
		[HttpPost("Update")]
		[HrmsPermission(HrmsPermissionAttribute.Onboarding, "Edit")]
		[RequestSizeLimit(20 * 1024 * 1024)]   // 2 passport files (5 MB each) + certificates (5 MB in total)
		public async Task<IActionResult> Update([FromForm] EmployeeDetailsUpdateRequest model)
		{
			if (model == null || model.SubmissionId <= 0)
				return BadRequest(new { message = "Invalid request." });

			var errors = EmployeeDetailsController.Validate(model, out var dob);
			if (errors.Count > 0)
				return BadRequest(new { message = errors.First().Value, errors });

			var employeeStatus = model.EmployeeStatus?.Trim();
			if (!string.IsNullOrEmpty(employeeStatus) && employeeStatus != "Active" && employeeStatus != "Inactive")
				return BadRequest(new { message = "Status must be Active or Inactive.", errors = new Dictionary<string, string> { ["employeeStatus"] = "Status must be Active or Inactive." } });

			// Files are optional on edit - only validated/replaced when sent
			var fileErrors = new Dictionary<string, string>();
			EmployeeDetailsController.UploadedFile passportCopy = null, passportPhoto = null;
			if (model.PassportCopy != null && model.PassportCopy.Length > 0)
				passportCopy = await EmployeeDetailsController.ReadFileAsync(model.PassportCopy, "Passport copy", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, fileErrors, "passportCopy");
			if (model.PassportPhoto != null && model.PassportPhoto.Length > 0)
				passportPhoto = await EmployeeDetailsController.ReadFileAsync(model.PassportPhoto, "Passport-size photo", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, fileErrors, "passportPhoto");
			if (fileErrors.Count > 0)
				return BadRequest(new { message = fileErrors.First().Value, errors = fileErrors });

			var exists = await _context.EmployeeDetailSubmissions.AnyAsync(s => s.SubmissionId == model.SubmissionId && !s.IsDeleted);
			if (!exists) return NotFound(new { message = "Submission not found." });

			// Certificates: delete the ones marked for removal, add the new ones.
			// Same rules as the public form: max 10 files and 5 MB in total (kept + new).
			var existingCerts = await _context.EmployeeCertificateResults
				.FromSqlRaw("EXEC GetEmployeeCertificates @SubmissionIds = @p0, @IncludeData = @p1", model.SubmissionId.ToString(), false)
				.AsNoTracking()
				.ToListAsync();

			var removeIds = (model.RemoveCertificateIds ?? new List<int>())
				.Distinct()
				.Where(rid => existingCerts.Any(c => c.CertificateId == rid))   // only certificates of this submission
				.ToList();
			var keptCerts = existingCerts.Where(c => !removeIds.Contains(c.CertificateId)).ToList();

			var newCertFiles = model.Certificates?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
			var certErrors = new Dictionary<string, string>();
			var newCerts = new List<EmployeeDetailsController.UploadedFile>();

			if (keptCerts.Count + newCertFiles.Count > EmployeeDetailsController.MaxCertificates)
				certErrors["certificates"] = $"A maximum of {EmployeeDetailsController.MaxCertificates} certificates is allowed.";
			else if (keptCerts.Sum(c => c.FileSize) + newCertFiles.Sum(f => f.Length) > EmployeeDetailsController.MaxCertificatesTotalBytes)
				certErrors["certificates"] = "The total size of all certificates must not exceed 5 MB.";
			else
			{
				for (int i = 0; i < newCertFiles.Count; i++)
				{
					var cert = await EmployeeDetailsController.ReadFileAsync(newCertFiles[i], $"Certificate {i + 1}", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, certErrors, "certificates");
					if (cert != null) newCerts.Add(cert);
				}
			}
			if (certErrors.Count > 0)
				return BadRequest(new { message = certErrors.First().Value, errors = certErrors });

			var passportNo = model.PassportNo.Trim().ToUpperInvariant();
			bool duplicate = await _context.EmployeeDetailSubmissions
				.AnyAsync(s => s.PassportNo == passportNo && s.SubmissionId != model.SubmissionId && !s.IsDeleted);
			if (duplicate)
				return Conflict(new { message = "Another submission already uses this passport number." });

			var parameters = new[]
			{
				new SqlParameter("@SubmissionId", model.SubmissionId),
				new SqlParameter("@FirstName", model.FirstName.Trim()),
				new SqlParameter("@LastName", model.LastName.Trim()),
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
				new SqlParameter("@SiemensIdReceivedMonth", SqlDbType.Date) { Value = model.HasSiemensIdCard?.Trim() == "Yes" ? (object?)EmployeeDetailsController.ParseMonthYear(model.SiemensIdReceivedMonth) ?? DBNull.Value : DBNull.Value },
				new SqlParameter("@WhatsAppNumber", EmployeeDetailsController.DbOrNull(model.WhatsAppNumber)),
				new SqlParameter("@EmployeeStatus", EmployeeDetailsController.DbOrNull(employeeStatus)),
				new SqlParameter("@UpdatedBy", HttpContext.User.Identity.Name),
				new SqlParameter("@PassportCopyFileName", (object?)passportCopy?.FileName ?? DBNull.Value),
				new SqlParameter("@PassportCopyContentType", (object?)passportCopy?.ContentType ?? DBNull.Value),
				new SqlParameter("@PassportCopyData", SqlDbType.VarBinary) { Value = (object?)passportCopy?.Content ?? DBNull.Value },
				new SqlParameter("@PassportPhotoFileName", (object?)passportPhoto?.FileName ?? DBNull.Value),
				new SqlParameter("@PassportPhotoContentType", (object?)passportPhoto?.ContentType ?? DBNull.Value),
				new SqlParameter("@PassportPhotoData", SqlDbType.VarBinary) { Value = (object?)passportPhoto?.Content ?? DBNull.Value }
			};

			// Details, passport files and certificates are saved together - all or nothing
			await using var tx = await _context.Database.BeginTransactionAsync();

			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC UpdateEmployeeDetails
				@SubmissionId, @FirstName, @LastName, @DateOfBirth, @PlaceOfBirth, @MobileNumber, @PassportNo, @SALCode,
				@City, @Country, @CurrentAddress, @NConsEmail, @NConsEmailStatus, @PersonalEmail, @VisaType, @WorkVisaExpiryDate, @HasSiemensIdCard, @SiemensIdReceivedMonth, @WhatsAppNumber, @EmployeeStatus, @UpdatedBy,
				@PassportCopyFileName, @PassportCopyContentType, @PassportCopyData,
				@PassportPhotoFileName, @PassportPhotoContentType, @PassportPhotoData",
				parameters
			);

			foreach (var certId in removeIds)
			{
				await _context.Database.ExecuteSqlRawAsync(
					"EXEC DeleteEmployeeDetailCertificate @CertificateId, @SubmissionId",
					new SqlParameter("@CertificateId", certId),
					new SqlParameter("@SubmissionId", model.SubmissionId));
			}

			foreach (var cert in newCerts)
			{
				await _context.Database.ExecuteSqlRawAsync(
					"EXEC InsertEmployeeDetailCertificate @SubmissionId, @FileName, @ContentType, @FileData",
					new SqlParameter("@SubmissionId", model.SubmissionId),
					new SqlParameter("@FileName", cert.FileName),
					new SqlParameter("@ContentType", cert.ContentType),
					new SqlParameter("@FileData", SqlDbType.VarBinary) { Value = cert.Content });
			}

			await tx.CommitAsync();

			return Ok(new { message = "Employee details updated successfully." });
		}

		// POST: api/EmployeeOnboarding/Delete   body: [1,2,3]
		// Soft delete: the submissions are flagged as deleted (data and files are kept in the database)
		[HttpPost("Delete")]
		[HrmsPermission(HrmsPermissionAttribute.Onboarding, "Edit")]
		public async Task<IActionResult> Delete([FromBody] int[] ids)
		{
			if (ids == null || ids.Length == 0)
				return BadRequest(new { message = "No records selected." });

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC DeleteEmployeeDetails @p0, @p1",
				string.Join(",", ids.Distinct()),
				HttpContext.User.Identity?.Name ?? (object)DBNull.Value
			);

			return Ok(new { message = "Deleted successfully." });
		}

		// GET: api/EmployeeOnboarding/ColumnSettings   -> { hiddenColumns: ["city", ...] }  (same for every user)
		[HttpGet("ColumnSettings")]
		public async Task<IActionResult> GetColumnSettings()
		{
			var hidden = await _context.HiddenColumnRows
				.FromSqlRaw("EXEC GetOnboardingHiddenColumns")
				.AsNoTracking()
				.ToListAsync();

			return Ok(new { hiddenColumns = hidden.Select(h => h.ColumnKey) });
		}

		// POST: api/EmployeeOnboarding/ColumnSettings   { hiddenColumns: [...] }   Admin / Manager only
		// Only changes which columns are shown - the employee data itself is never touched.
		[HttpPost("ColumnSettings")]
		public async Task<IActionResult> SaveColumnSettings([FromBody] ColumnSettingsRequest request)
		{
			var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
			if (!HrmsPermissionAttribute.IsAdminOrManager(role))
				return StatusCode(403, new { message = "Only Admin or Manager can change column visibility." });

			var keys = (request?.HiddenColumns ?? new List<string>())
				.Select(k => (k ?? "").Trim())
				.Where(k => k.Length > 0 && Regex.IsMatch(k, @"^[A-Za-z0-9_]{1,50}$"))
				.Where(k => !AlwaysVisibleColumns.Contains(k))      // Name and Actions can never be hidden
				.Distinct()
				.ToList();

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC SaveOnboardingHiddenColumns @p0, @p1",
				string.Join(",", keys),
				(object?)User.Identity?.Name ?? DBNull.Value);

			return Ok(new { message = "Column settings saved." });
		}

		// POST: api/EmployeeOnboarding/Download   { all: true }  or  { ids: [1,2] }
		// ZIP = Employee_Details.xlsx + one folder per employee holding the passport copy and photo
		[HttpPost("Download")]
		public async Task<IActionResult> Download([FromBody] EmployeeOnboardingDownloadRequest request)
		{
			bool all = request?.All == true;
			var ids = request?.Ids ?? Array.Empty<int>();
			if (!all && ids.Length == 0)
				return BadRequest(new { message = "No records selected." });

			var rows = await _context.EmployeeDetailLists
				.FromSqlRaw("EXEC GetEmployeeDetails")
				.AsNoTracking()
				.ToListAsync();

			if (!all) rows = rows.Where(r => ids.Contains(r.SubmissionId)).ToList();
			if (rows.Count == 0) return NotFound(new { message = "No records found." });

			var files = all
				? await _context.EmployeeDetailFileResults
					.FromSqlRaw("EXEC GetEmployeeDetailFiles")
					.AsNoTracking()
					.ToListAsync()
				: await _context.EmployeeDetailFileResults
					.FromSqlRaw("EXEC GetEmployeeDetailFiles @p0", string.Join(",", rows.Select(r => r.SubmissionId)))
					.AsNoTracking()
					.ToListAsync();

			var certificates = all
				? await _context.EmployeeCertificateResults
					.FromSqlRaw("EXEC GetEmployeeCertificates")
					.AsNoTracking()
					.ToListAsync()
				: await _context.EmployeeCertificateResults
					.FromSqlRaw("EXEC GetEmployeeCertificates @SubmissionIds = @p0", string.Join(",", rows.Select(r => r.SubmissionId)))
					.AsNoTracking()
					.ToListAsync();

			// the Excel follows the visible columns of the list (hidden ones are left out)
			var hiddenKeys = (await _context.HiddenColumnRows
				.FromSqlRaw("EXEC GetOnboardingHiddenColumns")
				.AsNoTracking()
				.ToListAsync()).Select(h => h.ColumnKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

			using var zipStream = new MemoryStream();
			using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
			{
				// 1) workbook with all details
				var entry = archive.CreateEntry("Employee_Details.xlsx", CompressionLevel.Fastest);
				using (var es = entry.Open())
				{
					var bytes = BuildWorkbook(rows, certificates, hiddenKeys);
					await es.WriteAsync(bytes, 0, bytes.Length);
				}

				// 2) attachments, one folder per employee
				foreach (var row in rows)
				{
					var folder = SafeName($"{row.FirstName}_{row.LastName}_{row.PassportNo}") + "/";
					var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var f in files.Where(x => x.SubmissionId == row.SubmissionId && x.FileData != null))
					{
						// Keep the original uploaded file name so it matches the Excel columns exactly.
						// Only if both files share the same name, the second one gets a suffix.
						var name = Path.GetFileName(f.FileName);
						if (!usedNames.Add(name))
						{
							var suffix = f.FileType == "PassportCopy" ? "_Passport_Copy" : "_Passport_Photo";
							name = Path.GetFileNameWithoutExtension(name) + suffix + Path.GetExtension(name);
							usedNames.Add(name);
						}

						var fe = archive.CreateEntry(folder + name, CompressionLevel.Fastest);
						using var fs = fe.Open();
						await fs.WriteAsync(f.FileData, 0, f.FileData.Length);
					}

					// certificates go into a "Certificates" sub-folder, original file names kept
					var certNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var cert in certificates.Where(x => x.SubmissionId == row.SubmissionId && x.FileData != null))
					{
						var cname = UniqueCertificateName(Path.GetFileName(cert.FileName), certNames);
						var ce = archive.CreateEntry(folder + "Certificates/" + cname, CompressionLevel.Fastest);
						using var cs = ce.Open();
						await cs.WriteAsync(cert.FileData, 0, cert.FileData.Length);
					}
				}
			}

			return File(zipStream.ToArray(), "application/zip",
				$"Employee_Details_{DateTime.Now:yyyyMMddHHmmss}.zip");
		}

		// ---------------------------------------------------------------------
		private static readonly string[] AlwaysVisibleColumns = { "fullName", "actions", "select" };

		private static string SafeName(string value) =>
			Regex.Replace(value ?? "", @"[^\w\-]+", "_").Trim('_');

		// Same name twice (e.g. two "certificate.pdf") -> certificate (2).pdf
		private static string UniqueCertificateName(string name, HashSet<string> used)
		{
			if (used.Add(name)) return name;
			var baseName = Path.GetFileNameWithoutExtension(name);
			var ext = Path.GetExtension(name);
			for (int n = 2; ; n++)
			{
				var candidate = $"{baseName} ({n}){ext}";
				if (used.Add(candidate)) return candidate;
			}
		}

		private static byte[] BuildWorkbook(List<EmployeeDetailList> rows, List<EmployeeCertificate> certificates, HashSet<string> hiddenKeys)
		{
			using var wb = new XLWorkbook();
			var ws = wb.Worksheets.Add("Employee Details");

			// certificate file names per employee, in the same (de-duplicated) form as inside the ZIP
			var certNames = rows.ToDictionary(x => x.SubmissionId, x =>
			{
				var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				return string.Join("; ", certificates
					.Where(cf => cf.SubmissionId == x.SubmissionId)
					.Select(cf => UniqueCertificateName(Path.GetFileName(cf.FileName), used)));
			});

			// key = the column key used by the Onboarding list. A column hidden there is left out here too.
			var columns = new List<(string Key, string Header, Func<EmployeeDetailList, string> Value)>
			{
				("fullName", "First Name", x => x.FirstName),
				("fullName", "Last Name", x => x.LastName),
				("dobText", "Date of Birth", x => x.DateOfBirth.ToString("dd/MM/yyyy")),
				("placeOfBirth", "Place of Birth", x => x.PlaceOfBirth),
				("mobileNumber", "Mobile Number", x => x.MobileNumber),
				("passportNo", "Passport No.", x => x.PassportNo),
				("salCode", "SAL Code", x => x.SALCode ?? ""),
				("city", "City", x => x.City),
				("country", "Country", x => x.Country),
				("currentAddress", "Current Address", x => x.CurrentAddress),
				("nConsEmail", "N-Cons Email ID", x => x.NConsEmail ?? ""),
				("nConsEmailStatus", "N-Cons Email Status", x => x.NConsEmailStatus ?? ""),
				("personalEmail", "Personal Email ID", x => x.PersonalEmail),
				("employeeStatus", "Status", x => x.EmployeeStatus ?? ""),
				("workStatus", "Work Status", x => x.WorkStatus ?? ""),
				("workPmName", "PM Name", x => x.WorkPmName ?? ""),
				("visaType", "Visa / Work Permit Type", x => x.VisaType ?? ""),
				("visaExpiryText", "Work Visa Expiry Date", x => x.WorkVisaExpiryDate?.ToString("dd/MM/yyyy") ?? ""),
				("hasSiemensIdCard", "Siemens ID Card", x => x.HasSiemensIdCard ?? ""),
				("siemensMonthText", "Siemens ID Received Month", x => x.SiemensIdReceivedMonth?.ToString("MMM yyyy") ?? ""),
				("whatsAppNumber", "WhatsApp Number", x => x.WhatsAppNumber ?? ""),
				("documents", "Passport Copy", x => x.PassportCopyFileName ?? ""),
				("documents", "Passport Photo", x => x.PassportPhotoFileName ?? ""),
				("certificateCount", "Certificates", x => certNames[x.SubmissionId]),
				("consentText", "Consent", x => x.ConsentGiven == true
					? "Yes" + (x.ConsentAt.HasValue ? " (" + x.ConsentAt.Value.ToString("dd/MM/yyyy HH:mm") + ")" : "")
					: "Not recorded"),
				("submittedText", "Submitted At", x => x.SubmittedAt.ToString("dd/MM/yyyy HH:mm"))
			}
			.Where(c => !hiddenKeys.Contains(c.Key))
			.ToList();

			for (int c = 0; c < columns.Count; c++)
			{
				var cell = ws.Cell(1, c + 1);
				cell.Value = columns[c].Header;
				cell.Style.Font.Bold = true;
				cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE9FE");
			}

			int r = 2;
			foreach (var x in rows)
			{
				for (int c = 0; c < columns.Count; c++)
					ws.Cell(r, c + 1).SetValue(columns[c].Value(x) ?? "");   // text only, e.g. keeps the leading + of phone numbers
				r++;
			}

			ws.Columns().AdjustToContents();
			using var ms = new MemoryStream();
			wb.SaveAs(ms);
			return ms.ToArray();
		}
	}
}
