namespace NatoHrmsBackend.Models
{
	public class HolidayResponse
	{
		public int? HolidayId { get; set; }
		public string EventName { get; set; }
		public DateTime EventDate { get; set; }
		public string EventType { get; set; }
		public string WorkLocation { get; set; }
	}
}
