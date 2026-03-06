namespace NatoHrmsBackend.Models
{
	public class JobApplicationRequest
	{
		public int JobId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Email { get; set; }
		public string Phone { get; set; }
		public string Skills { get; set; }
		public IFormFile Resume { get; set; }          // file
		public string ResumeFileName { get; set; }
		public string ResumeFileType { get; set; }
		public string CandidateStatus { get; set; }    // e.g., "New"
	}
}
