namespace NatoHrmsBackend.Models
{
	public class AssignedEmployeeResponse
	{
		public string? EmployeeId { get; set; }
		public string? EmployeeName { get; set; }
		public string? Email { get; set; }
		public string? Designation { get; set; }
		public string? Department { get; set; }   // ← added
		public string? ProjectName { get; set; }
		public string? AssignedDate { get; set; }
		public string? Status { get; set; }
	}
}
