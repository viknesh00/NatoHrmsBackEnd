namespace NatoHrmsBackend.Models
{
	public class JobApplicationResponse
	{
		public int JobId { get; set; }
		public string FirstName { get; set; } = null!;
		public string LastName { get; set; } = null!;
		public string Email { get; set; } = null!;
		public string? Phone { get; set; }
		public string Skills { get; set; } = null!;
		public IFormFile Resume { get; set; } = null!;
	}
}
