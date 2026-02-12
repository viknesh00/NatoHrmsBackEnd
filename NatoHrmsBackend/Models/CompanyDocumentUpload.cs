namespace NatoHrmsBackend.Models
{
	public class CompanyDocumentUpload
	{
		public int? Id { get; set; }
		public string DocumentName { get; set; }
		public string Tags { get; set; } // comma-separated
		public string AssignedPeople { get; set; } // comma-separated
		public DateTime? ReviewDate { get; set; }
		public bool IsCurrent { get; set; }
		public bool? RemoveExistingFile { get; set; }

		public IFormFile? Document { get; set; }
	}

}
