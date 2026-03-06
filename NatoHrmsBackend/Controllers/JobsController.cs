using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NatoHrmsBackend.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class JobsController : ControllerBase
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmailService _emailService;
		private readonly IConfiguration _config;
		private readonly ILogger<EmailService> _logger;

		public JobsController(ApplicationDbContext context, IConfiguration config, IEmailService emailService, ILogger<EmailService> logger)
		{
			_context = context;
			_config = config;
			_emailService = emailService;
			_logger = logger;
		}

		// Allow anonymous access for this endpoint
		[AllowAnonymous]
		[HttpGet("GetJobs")]
		public async Task<IActionResult> GetJobs()
		{
			var jobs = await _context.JobResponses
				.FromSqlRaw("EXEC sp_GetAllJobs")
				.ToListAsync();

			return Ok(jobs);
		}

		[HttpGet("GetJobById")]
		[Authorize] // still requires auth
		public async Task<IActionResult> GetJobById(int jobId)
		{
			var job = await _context.JobResponses
				.FromSqlRaw("EXEC sp_GetJobById @p0", jobId)
				.ToListAsync();

			return Ok(job);
		}

		[HttpPost("CreateJob")]
		[Authorize]
		public async Task<IActionResult> CreateJob(JobResponse job)
		{
			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC sp_InsertJob 
        @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8",
				job.JobTitle,
				job.Role,
				job.Location,
				job.Salary,
				job.JobType,
				job.Skills,
				job.Description,
				job.Responsibilities,
				job.Qualifications
			);

			return Ok("Job Created Successfully");
		}

		[HttpPut("UpdateJob")]
		[Authorize]
		public async Task<IActionResult> UpdateJob(JobResponse job)
		{
			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC sp_UpdateJob 
        @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9",
				job.JobId,
				job.JobTitle,
				job.Role,
				job.Location,
				job.Salary,
				job.JobType,
				job.Skills,
				job.Description,
				job.Responsibilities,
				job.Qualifications
			);

			return Ok("Job Updated Successfully");
		}

		[HttpDelete("DeleteJob")]
		[Authorize]
		public async Task<IActionResult> DeleteJob(int jobId)
		{
			await _context.Database.ExecuteSqlRawAsync(
				"EXEC sp_DeleteJob @p0",
				jobId
			);

			return Ok("Job Deleted Successfully");
		}

		[HttpGet]
		[Authorize]
		public async Task<IActionResult> GetAllApplications()
		{
			var applications = await _context.JobApplications
				.FromSqlRaw("EXEC GetJobApplications")
				.ToListAsync();

			return Ok(applications);
		}

		[HttpGet("Download/{id}")]
		[Authorize]
		public async Task<IActionResult> Download(int id)
		{
			var docs = await _context.Set<CompanyDocumentDownload>()
				.FromSqlRaw("EXEC DownloadResume @p0", id)
				.AsNoTracking()
				.ToListAsync();

			var doc = docs.FirstOrDefault();

			if (doc == null || doc.FileData == null)
				return NotFound();

			return File(doc.FileData, doc.ContentType, doc.FileName);
		}

		// 🔹 DOWNLOAD MULTIPLE (ZIP)
		[HttpPost("DownloadMultiple")]
		[Authorize]
		public async Task<IActionResult> DownloadMultiple([FromBody] int[] ids)
		{
			if (ids == null || ids.Length == 0)
				return BadRequest("No documents selected.");

			var idString = string.Join(",", ids);

			var documents = await _context.CompanyDocumentDownloads
				.FromSqlRaw("EXEC DownloadMultipleResume @p0", idString)
				.AsNoTracking()
				.ToListAsync();

			if (!documents.Any())
				return NotFound("No documents found.");

			using var zipStream = new MemoryStream();
			using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
			{
				foreach (var doc in documents)
				{
					var entry = archive.CreateEntry(doc.FileName, CompressionLevel.Fastest);
					using var entryStream = entry.Open();
					await entryStream.WriteAsync(doc.FileData, 0, doc.FileData.Length);
				}
			}

			zipStream.Position = 0;

			return File(
				zipStream.ToArray(),
				"application/zip",
				$"CompanyDocuments_{DateTime.Now:yyyyMMddHHmmss}.zip"
			);
		}

		[HttpPost("UpdateApplicationStaus")]
		[Authorize]
		public async Task<IActionResult> UpdateIsActive([FromBody] UpdateApplicationStatus request)
		{

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC UpdateCandidateStatus @p0, @p1,@p2",
				request.ApplicationId,
				request.CandidateStatus,
				request.AssignedTo
			);

			return Ok(new { Message = "Status updated successfully." });
		}

		[AllowAnonymous]
		[HttpPost("SubmitApplication")]
		public async Task<IActionResult> SubmitApplication([FromForm] JobApplicationResponse job)
		{
			if (job.Resume == null || job.Resume.Length == 0)
				return BadRequest("Resume file is required.");

			// Convert file to byte[]
			byte[] fileData;
			using (var ms = new MemoryStream())
			{
				await job.Resume.CopyToAsync(ms);
				fileData = ms.ToArray();
			}

			// Insert into DB
			await _context.Database.ExecuteSqlRawAsync(
				@"EXEC InsertJobApplication 
            @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8",
				job.JobId,
				job.FirstName,
				job.LastName,
				job.Email,
				job.Phone ?? (object)DBNull.Value,
				job.Skills,
				fileData,
				job.Resume.FileName,
				job.Resume.ContentType
			);

			// Fetch Job Title & Location
			var jobInfo = await _context.Jobs
				.Where(j => j.JobId == job.JobId)
				.Select(j => new { j.JobTitle, j.Location })
				.FirstOrDefaultAsync();

			string jobTitle = jobInfo?.JobTitle ?? "N/A";
			string location = jobInfo?.Location ?? "N/A";

			// Prepare professional email content
			string subject = $"New Job Application: {jobTitle}";
			string body = $@"
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
  <tr>
    <td align='center'>
      <table width='600' cellpadding='20' cellspacing='0' border='0' style='border:1px solid #ddd;'>
        <tr>
          <td>
            <h2 style='color:#2E86C1; margin:0 0 10px 0;'>{subject}</h2>
            <p style='margin:5px 0;'>Dear Recruitment Team,</p>
            <p style='margin:10px 0;'>A new job application has been submitted for the position <strong>{jobTitle}</strong> at <strong>{location}</strong>.</p>
            <h4>Applicant Details:</h4>
            <ul>
                <li><strong>Name:</strong> {job.FirstName} {job.LastName}</li>
                <li><strong>Email:</strong> {job.Email}</li>
                <li><strong>Phone:</strong> {job.Phone ?? "N/A"}</li>
                <li><strong>Primary Skills:</strong> {job.Skills}</li>
            </ul>
            <p>Please review the attached resume.</p>
            <hr style='border:none; border-top:1px solid #ddd; margin:20px 0;' />
            <p style='color:gray; font-size:12px; margin:5px 0;'>This is an automated notification from the HRMS system.</p>
            <p style='margin:10px 0;'>Regards,<br/>HRMS Team</p>
            <table cellpadding='0' cellspacing='0' border='0' style='margin-top:10px;'>
              <tr>
                <td>
                  <img src='https://www.natobotics.com/img/Natobotics.png' alt='Company Logo' width='50' style='display:block;' />
                </td>
                <td style='padding-left:10px; font-size:12px; color:#333;'>
                  e: hr@natobotics.com<br/>
                  a: Natobotics Technologies Pvt Ltd, Tidel Park, Taramani, Chennai-600113<br/>
                  w: <a href='https://www.natobotics.com' style='color:#2E86C1; text-decoration:none;'>www.natobotics.com</a>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

			try
			{
				// Send email to hardcoded address from appsettings
				var toEmail = _config["Jobportal"]; // e.g., "recruitment@natobotics.com"
				await _emailService.SendEmail(
					to: toEmail,
					subject: subject,
					body: body,
					 attachments: new[] {
						new EmailAttachment {
						FileName = job.Resume.FileName,
						ContentType = job.Resume.ContentType,
						Content = fileData
						}
					}
				);

				// Auto reply email to applicant
				string applicantSubject = $"Application Received – {jobTitle} | Natobotics Technologies";

				string applicantBody = $@"
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
  <tr>
    <td align='center'>
      <table width='600' cellpadding='20' cellspacing='0' border='0' style='border:1px solid #ddd;'>

        <tr>
          <td>

            <h2 style='color:#2E86C1;margin-bottom:10px;'>Thank you for your application</h2>

            <p>Dear {job.FirstName},</p>

            <p>
            Thank you for applying for the position of 
            <strong>{jobTitle}</strong> at <strong>Natobotics Technologies Pvt Ltd</strong>.
            </p>

            <p>
            We have successfully received your application and our recruitment team will review your profile carefully.
            If your qualifications match our current requirements, our team will contact you for the next steps in the recruitment process.
            </p>

            <p>
            We appreciate your interest in building your career with Natobotics and taking the time to submit your application.
            </p>

            <p>
            Please note that this is an <strong>automated system-generated email</strong>. 
            Kindly do not reply directly to this message.
            </p>

            <p>
            For any queries, you may contact us at 
            <a href='mailto:info@natobotics.com'>info@natobotics.com</a>.
            </p>

            <br/>

            <p>Best Regards,<br/>
            <strong>Recruitment Team</strong><br/>
            Natobotics Technologies Pvt Ltd</p>

            <hr style='border:none;border-top:1px solid #ddd;margin:20px 0;'>

            <table cellpadding='0' cellspacing='0' border='0'>
              <tr>
                <td>
                  <img src='https://www.natobotics.com/img/Natobotics.png' width='50'/>
                </td>

                <td style='padding-left:10px;font-size:12px;color:#555;'>
                  Natobotics Technologies Pvt Ltd<br/>
                  Tidel Park, Taramani, Chennai – 600113<br/>
                  <a href='https://www.natobotics.com'>www.natobotics.com</a>
                </td>

              </tr>
            </table>

            <p style='color:gray;font-size:12px;margin-top:10px;'>
            This is a system generated email. Please do not reply to this email.
            </p>

          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>";
				await _emailService.SendEmail(
	to: job.Email,
	subject: applicantSubject,
	body: applicantBody
);
			}
			catch (Exception ex)
			{
				// Log but don't block API
				_logger.LogError(ex, "Failed to send job application email");
			}

			return Ok("Application submitted successfully.");
		}
	}
}