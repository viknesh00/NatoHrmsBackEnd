namespace NatoHrmsBackend.Models
{
	// One work status record joined with the employee details (list / report)
	public class WorkStatusRow
	{
		public int WorkStatusId { get; set; }
		public int SubmissionId { get; set; }
		public string SALCode { get; set; }
		public string PMName { get; set; }
		public string? PMEmail { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public string WorkStatus { get; set; }       // Active / Deactive

		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string PassportNo { get; set; }
		public string? City { get; set; }
		public string? Country { get; set; }
		public string? EmployeeStatus { get; set; } // Active / Inactive
		public string? HasSiemensIdCard { get; set; }
	}
}
