using System;

namespace NatoHrmsBackend.Models
{
	public class Attendance
	{
		public int AttendanceId { get; set; }
		public string UserEmail { get; set; }
		public DateTime? ClockIn { get; set; }
		public DateTime? ClockOut { get; set; }
		public string ClockInIp { get; set; }
		public string ClockOutIp { get; set; }
		public string ClockInLocation { get; set; }
		public string ClockOutLocation { get; set; }
		public DateTime AttendanceDate { get; set; }
	}
}
