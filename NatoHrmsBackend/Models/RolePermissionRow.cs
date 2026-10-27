namespace NatoHrmsBackend.Models
{
	// Read / Write / Edit rights of one role for one functionality (Onboarding, WorkStatus)
	public class RolePermissionRow
	{
		public string Role { get; set; }
		public string Resource { get; set; }
		public bool CanRead { get; set; }
		public bool CanWrite { get; set; }
		public bool CanEdit { get; set; }
	}
}
