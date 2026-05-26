namespace NatoHrmsBackend.Models
{
	public class ProjectResponse
	{
		public int ProjectId { get; set; }
		public string? ProjectName { get; set; }  // string → string?
		public string? Description { get; set; }
		public string? ClientName { get; set; }
		public string? ManagerEmail { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public string? Status { get; set; }  // string → string?
		public DateTime? CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}
}
