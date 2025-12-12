namespace NatoHrmsBackend.Models
{
	public class TimeSheetResponse
	{
		public int TimesheetId { get; set; }
		public int UserId { get; set; }
		public string EmployeeId { get; set; }
		public string EmployeeName { get; set; }
		public string Username { get; set; }
		public DateTime EntryDate { get; set; }
		public string? TaskDetails { get; set; }
		public decimal WorkingHours { get; set; }
		public string? LeaveType { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}
}
