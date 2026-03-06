namespace NatoHrmsBackend.Models
{

	public class JobResponse
	{
		public int JobId { get; set; }
		public string JobCode { get; set; }
		public string Role { get; set; }
		public string JobTitle { get; set; }
		public string Location { get; set; }
		public string Salary { get; set; }
		public string JobType { get; set; }
		public string Skills { get; set; }

		public string Description { get; set; }
		public string Responsibilities { get; set; }
		public string Qualifications { get; set; }

		public DateTime PostedDate { get; set; }
	}
}
