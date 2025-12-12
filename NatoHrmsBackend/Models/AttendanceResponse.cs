namespace NatoHrmsBackend.Models
{
	public class AttendanceResponse
	{
		public long SlNo { get; set; }
		public string Time { get; set; }
		public string Type { get; set; }
		public string IpAddress { get; set; }
		public string Location { get; set; }
		public string UserName { get; set; }
	}
}
