namespace NatoHrmsBackend.Models
{
	public class Project
	{
		public int ProjectId { get; set; }
		public string ProjectName { get; set; }
		public string? Description { get; set; }
		public string? ClientName { get; set; }
		public string? ManagerEmail { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public string Status { get; set; } = "Active";
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }

	}
}
