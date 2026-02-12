namespace NatoHrmsBackend.Models
{
	public class CompanyDocumentList
	{
		public int Id { get; set; }

		public string DocumentName { get; set; }
		public string Tags { get; set; }
		public string AssignedPeople { get; set; }   // ✅ ADD

		public string FileName { get; set; }         // ✅ ADD (for edit + preview)
		public string ContentType { get; set; }      // ✅ ADD (optional but useful)

		public int AssignedCount { get; set; }
		public int ReadCount { get; set; }

		public DateTime? ReviewDate { get; set; }
		public bool IsCurrent { get; set; }

		public DateTime CreatedDate { get; set; }    // ✅ ADD
		public DateTime LastUpdated { get; set; }
	}
}
