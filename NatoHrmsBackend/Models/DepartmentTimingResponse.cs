namespace NatoHrmsBackend.Models
{
	public class DepartmentTimingResponse
	{
		public int DeptId { get; set; }
		public string DepartmentName { get; set; }
		public TimeSpan StartTime { get; set; }
		public TimeSpan EndTime { get; set; }
	}
}
