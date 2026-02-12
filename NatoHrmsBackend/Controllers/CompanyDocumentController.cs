using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using System.Data;
using System.IO.Compression;

namespace NatoHrmsBackend.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/[controller]")]
	public class CompanyDocumentController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public CompanyDocumentController(ApplicationDbContext context)
		{
			_context = context;
		}

		// 🔹 GET ALL
		[HttpGet("GetAll")]
		public async Task<IActionResult> GetAll()
		{
			string userName = HttpContext.User.Identity.Name;
			var result = await _context.Set<CompanyDocumentList>()
				.FromSqlRaw("EXEC GetCompanyDocuments @p0", userName)
				.AsNoTracking()
				.ToListAsync();

			return Ok(result);
		}

		// 🔹 DOWNLOAD (increments read count)
		[HttpGet("Download/{id}")]
		public async Task<IActionResult> Download(int id)
		{
			var docs = await _context.Set<CompanyDocumentDownload>()
				.FromSqlRaw("EXEC DownloadCompanyDocument @p0", id)
				.AsNoTracking()
				.ToListAsync();

			var doc = docs.FirstOrDefault();

			if (doc == null || doc.FileData == null)
				return NotFound();

			return File(doc.FileData, doc.ContentType, doc.FileName);
		}

		// 🔹 DOWNLOAD MULTIPLE (ZIP)
		[HttpPost("DownloadMultiple")]
		public async Task<IActionResult> DownloadMultiple([FromBody] int[] ids)
		{
			if (ids == null || ids.Length == 0)
				return BadRequest("No documents selected.");

			var idString = string.Join(",", ids);

			var documents = await _context.CompanyDocumentDownloads
				.FromSqlRaw("EXEC DownloadMultipleCompanyDocuments @p0", idString)
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

		// 🔹 DELETE (single / multi)
		[HttpPost("Delete")]
		public async Task<IActionResult> Delete([FromBody] int[] ids)
		{
			if (ids == null || ids.Length == 0)
				return BadRequest("No documents selected.");

			var idString = string.Join(",", ids);

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC DeleteCompanyDocuments @p0", idString
			);

			return Ok(new { message = "Document(s) deleted successfully" });
		}

		// 🔹 SAVE / UPDATE
		[HttpPost("SaveDocument")]
		public async Task<IActionResult> SaveDocument([FromForm] CompanyDocumentUpload model)
		{
			if (string.IsNullOrWhiteSpace(model.DocumentName))
				return BadRequest("Document Name is required");

			byte[] fileData = null;
			string fileName = null;
			string contentType = null;

			if (model.Document != null && model.Document.Length > 0)
			{
				using var ms = new MemoryStream();
				await model.Document.CopyToAsync(ms);
				fileData = ms.ToArray();
				fileName = model.Document.FileName;
				contentType = model.Document.ContentType;
			}

			var parameters = new[]
			{
		new SqlParameter("@Id", model.Id ?? (object)DBNull.Value),

		new SqlParameter("@DocumentName", model.DocumentName),

		new SqlParameter("@Tags", (object?)model.Tags ?? DBNull.Value),

		new SqlParameter("@AssignedPeople", (object?)model.AssignedPeople ?? DBNull.Value),

		new SqlParameter("@ReviewDate", model.ReviewDate ?? (object)DBNull.Value),

		new SqlParameter("@IsCurrent", model.IsCurrent),

		new SqlParameter("@RemoveExistingFile", model.RemoveExistingFile ?? false),

		new SqlParameter("@FileName", (object?)fileName ?? DBNull.Value),

		new SqlParameter("@ContentType", (object?)contentType ?? DBNull.Value),

        // 🔥 THIS IS THE IMPORTANT FIX
        new SqlParameter("@FileData", SqlDbType.VarBinary)
		{
			Value = (object?)fileData ?? DBNull.Value
		}
	};

			await _context.Database.ExecuteSqlRawAsync(
				"EXEC InsertOrUpdateCompanyDocument " +
				"@Id, @DocumentName, @Tags, @AssignedPeople, @ReviewDate, @IsCurrent, " +
				"@RemoveExistingFile, @FileName, @ContentType, @FileData",
				parameters
			);

			return Ok(new { message = "Document saved/updated successfully" });
		}

		// 🔹 PREVIEW
		[HttpGet("Preview/{id}")]
		public async Task<IActionResult> Preview(int id)
		{
			var docs = await _context.CompanyDocumentDownloads
				.FromSqlRaw(
					"EXEC PreviewCompanyDocument @p0", id
				)
				.AsNoTracking()
				.ToListAsync();

			var doc = docs
				.Select(x => new
				{
					x.FileData,
					x.FileName,
					x.ContentType
				})
				.FirstOrDefault();

			if (doc == null || doc.FileData == null || doc.FileData.Length == 0)
				return NotFound("File not found or empty");

			return File(
				doc.FileData,
				doc.ContentType,
				doc.FileName
			);
		}
	}
}
