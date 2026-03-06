using Microsoft.AspNetCore.Routing.Matching;

namespace NatoHrmsBackend.Models
{
	public class JobApplication
	{
		// Application fields
		public int ApplicationId { get; set; }
		public int JobId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Email { get; set; }
		public string? Phone { get; set; }
		public string Skills { get; set; }
		public int? ReadCount { get; set; }
		public string? CandidateStatus { get; set; }
		public string? AssignedTo { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public DateTime AppliedOn { get; set; }

		// Resume info
		public string ResumeFileName { get; set; }
		public string ResumeFileType { get; set; }

		// Job info
		public string Role { get; set; }
		public string JobTitle { get; set; }
		public string Salary { get; set; }
		public string JobType { get; set; }
		public string Location { get; set; }
	}
}
