namespace NatoHrmsBackend.Models
{
	public class ApproveLeaveRequest
	{
		public int LeaveId { get; set; }
		public bool IsApproved { get; set; }
		public string? ApproverReason { get; set; }
	}
}
