namespace NatoHrmsBackend.Models
{
	public class CompanyDocumentDownload
	{
			public string FileName { get; set; }
			public string ContentType { get; set; }
			public byte[] FileData { get; set; }

	}
}
