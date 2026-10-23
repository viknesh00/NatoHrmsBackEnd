namespace NatoHrmsBackend.Models
{
	// One uploaded certificate (a submission can have several)
	public class EmployeeCertificate
	{
		public int CertificateId { get; set; }
		public int SubmissionId { get; set; }
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public long FileSize { get; set; }
		public byte[]? FileData { get; set; }     // null when only the list is requested
	}
}
