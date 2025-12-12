namespace NatoHrmsBackend.Models
{
	public class UserLeaveRequest
	{
		public int? LeaveId { get; set; }        
		public string UserName { get; set; }   
		public string EmployeeName { get; set; }
		public string? ApprovedBy { get; set; }
		public DateTime FromDate { get; set; }
		public DateTime ToDate { get; set; }
		public string DayType { get; set; }
		public string LeaveType { get; set; }
		public string Reason { get; set; }
		public bool CancelLeave { get; set; }
		public bool IsApproved { get; set; }
	}
}
