namespace NatoHrmsBackend.Models
{
	public class EmployeeDetailSubmission
	{
		public int SubmissionId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public DateTime DateOfBirth { get; set; }
		public string PlaceOfBirth { get; set; }
		public string MobileNumber { get; set; }
		public string PassportNo { get; set; }
		public string? SALCode { get; set; }
		public string City { get; set; }
		public string Country { get; set; }
		public string CurrentAddress { get; set; }
		public string? NConsEmail { get; set; }
		public string? NConsEmailStatus { get; set; }
		public string PersonalEmail { get; set; }
		public string? SubmittedFromIp { get; set; }
		public DateTime SubmittedAt { get; set; }
		public bool IsDeleted { get; set; }
	}
}
