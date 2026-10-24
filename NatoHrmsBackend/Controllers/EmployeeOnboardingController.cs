using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Data;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NatoHrmsBackend.Controllers
{
	// HRMS screen for the submissions received from the public employee details form.
	// Menu visibility (department / role) is managed in the frontend Sidebar, like the other screens.
	[Authorize]
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

		// POST: api/EmployeeOnboarding/Update  (details + optional replacement files)
		[HttpPost("Update")]
		[RequestSizeLimit(12 * 1024 * 1024)]
		public async Task<IActionResult> Update([FromForm] EmployeeDetailsUpdateRequest model)
		{
			if (model == null || model.SubmissionId <= 0)
				return BadRequest(new { message = "Invalid request." });

			var errors = EmployeeDetailsController.Validate(model, out var dob);
			if (errors.Count > 0)
				return BadRequest(new { message = errors.First().Value, errors });

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
				new SqlParameter("@UpdatedBy", HttpContext.User.Identity.Name),
				new SqlParameter("@PassportCopyFileName", (object?)passportCopy?.FileName ?? DBNull.Value),
				new SqlParameter("@PassportCopyContentType", (object?)passportCopy?.ContentType ?? DBNull.Value),
				new SqlParameter("@PassportCopyData", SqlDbType.VarBinary) { Value = (object?)passportCopy?.Content ?? DBNull.Value },
				new SqlParameter("@PassportPhotoFileName", (object?)passportPhoto?.FileName ?? DBNull.Value),
				new SqlParameter("@PassportPhotoContentType", (object?)passportPhoto?.ContentType ?? DBNull.Value),
				new SqlParameter("@PassportPhotoData", SqlDbType.VarBinary) { Value = (object?)passportPhoto?.Content ?? DBNull.Value }
			};

			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC UpdateEmployeeDetails
				@SubmissionId, @FirstName, @LastName, @DateOfBirth, @PlaceOfBirth, @MobileNumber, @PassportNo, @SALCode,
				@City, @Country, @CurrentAddress, @NConsEmail, @NConsEmailStatus, @PersonalEmail, @VisaType, @WorkVisaExpiryDate, @HasSiemensIdCard, @SiemensIdReceivedMonth, @WhatsAppNumber, @UpdatedBy,
				@PassportCopyFileName, @PassportCopyContentType, @PassportCopyData,
				@PassportPhotoFileName, @PassportPhotoContentType, @PassportPhotoData",
				parameters
			);

			return Ok(new { message = "Employee details updated successfully." });
		}

		// POST: api/EmployeeOnboarding/Delete   body: [1,2,3]
		// Soft delete: the submissions are flagged as deleted (data and files are kept in the database)
		[HttpPost("Delete")]
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

			using var zipStream = new MemoryStream();
			using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
			{
				// 1) workbook with all details
				var entry = archive.CreateEntry("Employee_Details.xlsx", CompressionLevel.Fastest);
				using (var es = entry.Open())
				{
					var bytes = BuildWorkbook(rows, certificates);
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

		private static byte[] BuildWorkbook(List<EmployeeDetailList> rows, List<EmployeeCertificate> certificates)
		{
			using var wb = new XLWorkbook();
			var ws = wb.Worksheets.Add("Employee Details");

			var headers = new[]
			{
				"First Name", "Last Name", "Date of Birth", "Place of Birth", "Mobile Number", "Passport No.",
				"SAL Code", "City", "Country", "Current Address", "N-Cons Email ID", "N-Cons Email Status",
				"Personal Email ID", "Visa / Work Permit Type", "Work Visa Expiry Date", "Siemens ID Card", "Siemens ID Received Month", "WhatsApp Number",
				"Passport Copy", "Passport Photo", "Certificates", "Consent", "Submitted At"
			};
			for (int c = 0; c < headers.Length; c++)
			{
				var cell = ws.Cell(1, c + 1);
				cell.Value = headers[c];
				cell.Style.Font.Bold = true;
				cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE9FE");
			}

			int r = 2;
			foreach (var x in rows)
			{
				ws.Cell(r, 1).Value = x.FirstName;
				ws.Cell(r, 2).Value = x.LastName;
				ws.Cell(r, 3).Value = x.DateOfBirth.ToString("dd/MM/yyyy");
				ws.Cell(r, 4).Value = x.PlaceOfBirth;
				ws.Cell(r, 5).SetValue(x.MobileNumber);       // keep as text (leading +)
				ws.Cell(r, 6).SetValue(x.PassportNo);
				ws.Cell(r, 7).SetValue(x.SALCode ?? "");
				ws.Cell(r, 8).Value = x.City;
				ws.Cell(r, 9).Value = x.Country;
				ws.Cell(r, 10).Value = x.CurrentAddress;
				ws.Cell(r, 11).Value = x.NConsEmail ?? "";
				ws.Cell(r, 12).Value = x.NConsEmailStatus ?? "";
				ws.Cell(r, 13).Value = x.PersonalEmail;
				ws.Cell(r, 14).Value = x.VisaType ?? "";
				ws.Cell(r, 15).Value = x.WorkVisaExpiryDate?.ToString("dd/MM/yyyy") ?? "";
				ws.Cell(r, 16).Value = x.HasSiemensIdCard ?? "";
				ws.Cell(r, 17).Value = x.SiemensIdReceivedMonth?.ToString("MMM yyyy") ?? "";
				ws.Cell(r, 18).SetValue(x.WhatsAppNumber ?? "");
				ws.Cell(r, 19).Value = x.PassportCopyFileName ?? "";
				ws.Cell(r, 20).Value = x.PassportPhotoFileName ?? "";
				// certificate file names, in the same (de-duplicated) form as inside the ZIP
				var excelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				ws.Cell(r, 21).Value = string.Join("; ", certificates
					.Where(cf => cf.SubmissionId == x.SubmissionId)
					.Select(cf => UniqueCertificateName(Path.GetFileName(cf.FileName), excelNames)));
				ws.Cell(r, 22).Value = x.ConsentGiven == true
					? "Yes" + (x.ConsentAt.HasValue ? " (" + x.ConsentAt.Value.ToString("dd/MM/yyyy HH:mm") + ")" : "")
					: "Not recorded";
				ws.Cell(r, 23).Value = x.SubmittedAt.ToString("dd/MM/yyyy HH:mm");
				r++;
			}

			ws.Columns().AdjustToContents();
			using var ms = new MemoryStream();
			wb.SaveAs(ms);
			return ms.ToArray();
		}
	}
}
