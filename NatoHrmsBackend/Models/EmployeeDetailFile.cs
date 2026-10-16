namespace NatoHrmsBackend.Models
{
	public class EmployeeDetailFile
	{
		public int SubmissionId { get; set; }
		public string FileType { get; set; }      // PassportCopy | PassportPhoto
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public byte[] FileData { get; set; }
	}
}
