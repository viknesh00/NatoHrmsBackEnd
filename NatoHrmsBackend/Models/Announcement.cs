namespace NatoHrmsBackend.Models
{
	public class Announcement
	{
		public int? Id { get; set; }     // null for insert
		public string Department { get; set; }
		public string Description { get; set; }
		public DateTime AnnouncementDate { get; set; }
		public bool IsActive { get; set; }
		public int CreatedBy { get; set; }
		public int? UpdatedBy { get; set; }
	}
}
