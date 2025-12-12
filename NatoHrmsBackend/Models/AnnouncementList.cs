namespace NatoHrmsBackend.Models
{
	public class AnnouncementList
	{
		public int Id { get; set; }
		public string Department { get; set; }
		public string Description { get; set; }
		public DateTime AnnouncementDate { get; set; }
		public bool IsActive { get; set; }
		public string? CreatedBy { get; set; }
		public DateTime CreatedDate { get; set; }
		public string? UpdatedBy { get; set; }
		public DateTime? UpdatedDate { get; set; }
	}
}
