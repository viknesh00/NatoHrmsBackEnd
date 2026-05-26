namespace NatoHrmsBackend.Models
{
	public class InsertOrUpdateProjectRequest
	{
		public int? ProjectId { get; set; }
		public string ProjectName { get; set; }
		public string? Description { get; set; }
		public string? ClientName { get; set; }
		public string? ManagerEmail { get; set; }
		public string? StartDate { get; set; }
		public string? EndDate { get; set; }
		public string Status { get; set; } = "Active";
	}
}
