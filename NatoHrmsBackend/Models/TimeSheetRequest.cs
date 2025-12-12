namespace NatoHrmsBackend.Models
{
	public class TimeSheetRequest
	{
		public DateTime EntryDate { get; set; }
		public string? TaskDetails { get; set; }
		public decimal WorkingHours { get; set; }
		public string? LeaveType { get; set; }
	}
}
