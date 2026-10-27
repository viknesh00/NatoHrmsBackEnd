namespace NatoHrmsBackend.Models
{
	public class WorkStatusRequest
	{
		public int? WorkStatusId { get; set; }       // null = create, value = edit
		public int SubmissionId { get; set; }
		public string? SALCode { get; set; }
		public string? PMName { get; set; }
		public string? PMEmail { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public string? WorkStatus { get; set; }      // Active / Deactive
	}
}
