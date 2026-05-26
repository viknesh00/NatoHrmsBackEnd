namespace NatoHrmsBackend.Models
{
	public class AssignProjectRequest
	{
		public string Email { get; set; }
		public string ProjectName { get; set; }
		public string? AssignedDate { get; set; }   // "YYYY-MM-DD", optional
	}
}
