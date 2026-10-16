namespace NatoHrmsBackend.Models
{
	// Same fields as the public form; files are optional (kept as-is when not sent)
	public class EmployeeDetailsUpdateRequest : EmployeeDetailsRequest
	{
		public int SubmissionId { get; set; }
		public new IFormFile? PassportCopy { get; set; }
		public new IFormFile? PassportPhoto { get; set; }
	}
}
