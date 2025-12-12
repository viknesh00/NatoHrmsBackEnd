namespace NatoHrmsBackend.Models
{
	public class AttendanceMonthlyResponse
	{
		public string UserEmail { get; set; }
		public DateTime AttendanceDate { get; set; }

		public DateTime? FirstClockIn { get; set; }
		public DateTime? LatestClockOut { get; set; }

		public string? ClockInIp { get; set; }
		public string? ClockOutIp { get; set; }

		public string? ClockInLocation { get; set; }
		public string? ClockOutLocation { get; set; }

		public decimal? TotalWorkDuration { get; set; }

		public int? BreakCount { get; set; }

		public decimal? BreakDuration { get; set; }

		// Status includes: Present / Absent / Leave / Anomaly / WO
		public string Status { get; set; }
	}
}
