namespace NatoHrmsBackend.Models
{
	public class CompanyDocument
	{
		public int Id { get; set; }
		public string DocumentName { get; set; }
		public string Tags { get; set; }
		public int AssignedCount { get; set; }
		public int ReadCount { get; set; }
		public DateTime? ReviewDate { get; set; }
		public bool IsCurrent { get; set; }
		public DateTime LastUpdated { get; set; }

		public string FileName { get; set; }
		public string ContentType { get; set; }
		public byte[] FileData { get; set; }

		public bool IsDeleted { get; set; } = false;
	}

}
