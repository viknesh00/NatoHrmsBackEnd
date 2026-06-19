using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;
using NatoHrmsBackend.Models;
using NatoHrmsBackend.Services;

namespace NatoHrmsBackend.Services
{
	public class EmployeeTimesheetSummary
	{
		public int UserId { get; set; }
		public string EmployeeId { get; set; }
		public string EmployeeName { get; set; }
		public string Username { get; set; }
		public string Department { get; set; }
		public string ProjectAssigned { get; set; }
		public decimal TotalRegularHours { get; set; }
		public decimal TotalOvertimeHours { get; set; }
		public decimal TotalWorkingHours { get; set; }
		public Dictionary<string, int> LeaveCounts { get; set; } = new();
		public List<TimeSheetResponse> Entries { get; set; } = new();
	}

	public class MonthlyTimesheetReportService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<MonthlyTimesheetReportService> _logger;

		private const decimal MaxRegular = 8.0m;
		private string _lastSentMonth = "";

		public MonthlyTimesheetReportService(
			IServiceProvider serviceProvider,
			IConfiguration configuration,
			ILogger<MonthlyTimesheetReportService> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
		}

		// ── Scheduling (UTC) ────────────────────────────────────────────────

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Monthly Timesheet Report Service started (UTC mode)");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var now = DateTime.UtcNow;
					var lastWorkingDay = LastWorkingDayOfMonth(now);
					var sendHour = int.Parse(_configuration["TimesheetReport:SendHourLocal"] ?? "18");
					var monthKey = now.ToString("yyyy-MM");

					bool isReportDay = now.Date == lastWorkingDay.Date && now.Hour >= sendHour;
					//bool isReportDay = true;
					if (isReportDay && _lastSentMonth != monthKey)
					{
						await GenerateAndSendReport(monthKey);
						_lastSentMonth = monthKey;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error running MonthlyTimesheetReportService");
				}

				await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
				//await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
			}
		}

		private static bool IsWeekend(DateTime d) =>
			d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;

		private static DateTime LastWorkingDayOfMonth(DateTime anyDayInMonthUtc)
		{
			var lastDay = new DateTime(anyDayInMonthUtc.Year, anyDayInMonthUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
				.AddMonths(1).AddDays(-1);
			while (IsWeekend(lastDay))
				lastDay = lastDay.AddDays(-1);
			return lastDay;
		}

		// ── Report generation + send ────────────────────────────────────────

		private async Task GenerateAndSendReport(string monthKey)
		{
			using var scope = _serviceProvider.CreateScope();
			var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

			var department = _configuration["TimesheetReport:Department"] ?? "Deizeisau";
			var toEmail = _configuration["TimesheetReport:ToEmail"];
			var ccEmails = _configuration["TimesheetReport:CcEmails"];
			var bccEmails = _configuration["TimesheetReport:BccEmails"];

			if (string.IsNullOrWhiteSpace(toEmail))
			{
				_logger.LogWarning("TimesheetReport:ToEmail not configured — skipping monthly report send");
				return;
			}

			var rows = await context.TimeSheetResponses
				.FromSqlRaw("EXEC GetTimeSheetByDepartment @p0, @p1", monthKey, department)
				.ToListAsync();

			if (rows.Count == 0)
			{
				_logger.LogInformation("No timesheet rows found for {Dept} / {Month} — skipping send", department, monthKey);
				return;
			}

			var employees = GroupByUser(rows);
			var monthStart = DateTime.SpecifyKind(
				DateTime.ParseExact(monthKey + "-01", "yyyy-MM-dd", null),
				DateTimeKind.Utc);
			var excelBytes = BuildWorkbook(employees, monthStart);

			var fileName = $"Timesheet_{department}_{monthStart:MMM-yyyy}.xlsx";
			var attachment = new EmailAttachment
			{
				FileName = fileName,
				Content = excelBytes,
				ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
			};

			var totalRegular = employees.Sum(e => e.TotalRegularHours);
			var totalOvertime = employees.Sum(e => e.TotalOvertimeHours);
			var totalLeaveDays = employees.Sum(e => e.LeaveCounts.Values.Sum());

			var generatedAt = DateTime.UtcNow.ToString("dd MMM yyyy, HH:mm") + " UTC";

			var body = EmailTemplates.MonthlyTimesheetReportEmail(
				department,
				monthStart.ToString("MMMM yyyy"),
				employees.Count,
				(double)totalRegular,
				(double)totalOvertime,
				totalLeaveDays,
				generatedAt
			);

			var subject = $"{department} Monthly Timesheet Report — {monthStart:MMMM yyyy}";

			await emailService.SendEmail(
				to: toEmail,
				subject: subject,
				body: body,
				cc: ccEmails,
				bcc: bccEmails,
				attachments: new[] { attachment },
				includeCcFromConfig: false
			);

			_logger.LogInformation("Monthly timesheet report sent for {Dept} / {Month}", department, monthKey);
		}

		// ── Grouping helpers ─────────────────────────────────────────────────

		private static (decimal regular, decimal overtime) SplitHours(decimal total)
		{
			var regular = Math.Min(total, MaxRegular);
			var overtime = Math.Max(total - MaxRegular, 0);
			return (regular, overtime);
		}

		/// <summary>
		/// Converts decimal hours to H.MM string (e.g. 9.5h → "9.30"), matching React decimalToHHMM.
		/// </summary>
		private static string DecimalToHHMM(decimal h)
		{
			if (h <= 0) return "0.00";
			var hrs = (int)Math.Floor(h);
			var min = (int)Math.Round((h - hrs) * 60);
			return $"{hrs}.{min:D2}";
		}

		/// <summary>
		/// Writes hours as a formatted string "H.MM" into the cell, matching React string output.
		/// </summary>
		private static void SetHoursCell(IXLCell cell, decimal h)
		{
			cell.Value = DecimalToHHMM(h);
			cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
		}

		/// <summary>
		/// Bold, centre-aligned (H+V), wrap text, no background fill — matches React (no bg color).
		/// </summary>
		private static void StyleHeader(IXLRange range)
		{
			range.Style.Font.Bold = true;
			range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
			range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
			range.Style.Alignment.WrapText = true;
			range.Style.Fill.PatternType = XLFillPatternValues.None;
		}

		/// <summary>Collapses "Holiday: X" variants into a single "Holiday" bucket.</summary>
		private static string NormalizeLeaveType(string leaveType)
		{
			if (string.IsNullOrWhiteSpace(leaveType)) return leaveType;
			var trimmed = leaveType.Trim();
			return trimmed.StartsWith("Holiday", StringComparison.OrdinalIgnoreCase) &&
				   trimmed.IndexOf(':') >= 0
				? "Holiday"
				: trimmed;
		}

		private static List<EmployeeTimesheetSummary> GroupByUser(List<TimeSheetResponse> rows)
		{
			var grouped = new Dictionary<int, EmployeeTimesheetSummary>();

			foreach (var item in rows)
			{
				if (!grouped.TryGetValue(item.UserId, out var summary))
				{
					summary = new EmployeeTimesheetSummary
					{
						UserId = item.UserId,
						EmployeeId = item.EmployeeId,
						EmployeeName = item.EmployeeName,
						Username = item.Username,
						Department = item.Department,
						ProjectAssigned = item.ProjectAssigned,
					};
					grouped[item.UserId] = summary;
				}

				var dayTotal = item.WorkingHours;
				var (regular, overtime) = SplitHours(dayTotal);
				summary.Entries.Add(item);
				summary.TotalWorkingHours += dayTotal;
				summary.TotalRegularHours += regular;
				summary.TotalOvertimeHours += overtime;

				if (!string.IsNullOrWhiteSpace(item.LeaveType))
				{
					var key = NormalizeLeaveType(item.LeaveType);
					summary.LeaveCounts[key] = summary.LeaveCounts.GetValueOrDefault(key) + 1;
				}
			}

			return grouped.Values.ToList();
		}

		// ── Excel building ──────────────────────────────────────────────────

		private static byte[] BuildWorkbook(List<EmployeeTimesheetSummary> employees, DateTime monthStart)
		{
			using var wb = new XLWorkbook();

			// Fixed headers (columns 1-5)
			string[] fixedHeaders = { "Emp ID", "Employee Name", "Email", "Department", "Project Assigned" };

			// Leave type priority order — matches React LEAVE_ORDER
			var leaveTypesSet = employees.SelectMany(e => e.LeaveCounts.Keys).ToHashSet();
			var leaveOrder = new[] { "Holiday", "Sick Leave", "Casual Leave" };
			var leaveTypes = leaveOrder
				.Where(lt => leaveTypesSet.Contains(lt))
				.Concat(leaveTypesSet.Where(lt => !leaveOrder.Contains(lt)).OrderBy(x => x))
				.ToList();

			// Column layout (1-based):
			//   Cols 1-5          : fixed headers
			//   Cols 6..5+N       : one col per leave type
			//   Cols 6+N..8+N     : TOTAL (Regular | Overtime | Total)
			int leaveStartCol = 6;                             // 1-based
			int totalStartCol = leaveStartCol + leaveTypes.Count;
			int lastCol = totalStartCol + 2;

			// ════════════════════════════════════════════════════════════════
			// SHEET 1 — Leave Summary
			// ════════════════════════════════════════════════════════════════
			var leaveWs = wb.Worksheets.Add("Leave Summary");

			// Row 1: fixed headers in columns 1-5 (single row, NOT merged with row 2 — matches React)
			for (int i = 0; i < fixedHeaders.Length; i++)
			{
				var cell = leaveWs.Cell(1, i + 1);
				cell.Value = fixedHeaders[i];
				StyleHeader(leaveWs.Range(1, i + 1, 1, i + 1));
			}

			// Row 1: leave type headers (single row, NOT merged with row 2 — matches React)
			for (int i = 0; i < leaveTypes.Count; i++)
			{
				int col = leaveStartCol + i;
				var cell = leaveWs.Cell(1, col);
				cell.Value = leaveTypes[i];
				StyleHeader(leaveWs.Range(1, col, 1, col));
			}

			// Row 1: TOTAL group header — spans 3 columns (Regular | Overtime | Total)
			var totalHeader = leaveWs.Range(1, totalStartCol, 1, lastCol).Merge();
			totalHeader.Value = "TOTAL";
			StyleHeader(totalHeader);

			// Row 2: sub-headers under TOTAL only (fixed + leave cols have empty row 2)
			leaveWs.Cell(2, totalStartCol).Value = "Regular";
			leaveWs.Cell(2, totalStartCol + 1).Value = "Overtime";
			leaveWs.Cell(2, totalStartCol + 2).Value = "Total";
			StyleHeader(leaveWs.Range(2, totalStartCol, 2, lastCol));

			// Data rows start at row 3
			int row = 3;
			decimal grandReg = 0, grandOt = 0, grandAll = 0;

			foreach (var emp in employees)
			{
				leaveWs.Cell(row, 1).Value = emp.EmployeeId;
				leaveWs.Cell(row, 2).Value = emp.EmployeeName;
				leaveWs.Cell(row, 3).Value = emp.Username;
				leaveWs.Cell(row, 4).Value = emp.Department;
				leaveWs.Cell(row, 5).Value = emp.ProjectAssigned;

				for (int i = 0; i < leaveTypes.Count; i++)
					leaveWs.Cell(row, leaveStartCol + i).Value =
						emp.LeaveCounts.GetValueOrDefault(leaveTypes[i]);

				SetHoursCell(leaveWs.Cell(row, totalStartCol), emp.TotalRegularHours);
				SetHoursCell(leaveWs.Cell(row, totalStartCol + 1), emp.TotalOvertimeHours);
				SetHoursCell(leaveWs.Cell(row, totalStartCol + 2), emp.TotalWorkingHours);

				grandReg += emp.TotalRegularHours;
				grandOt += emp.TotalOvertimeHours;
				grandAll += emp.TotalWorkingHours;
				row++;
			}

			// Grand total row:
			// React layout: ["","","","", ...leaveTypes.map(()=>""), "TOTAL", reg, ot, all]
			// "TOTAL" label lands at column index (4 + leaveTypes.Count) = totalStartCol - 1 (1-based)
			// Columns 1-4 are blank, leave cols are blank, then "TOTAL" label, then 3 hour values.
			int totalLabelCol = totalStartCol - 1; // last leave column (or col 5 if no leave types)
			leaveWs.Cell(row, totalLabelCol).Value = "TOTAL";
			leaveWs.Cell(row, totalLabelCol).Style.Font.Bold = true;
			leaveWs.Cell(row, totalLabelCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

			SetHoursCell(leaveWs.Cell(row, totalStartCol), grandReg);
			SetHoursCell(leaveWs.Cell(row, totalStartCol + 1), grandOt);
			SetHoursCell(leaveWs.Cell(row, totalStartCol + 2), grandAll);
			leaveWs.Range(row, totalStartCol, row, lastCol).Style.Font.Bold = true;

			// Column widths — matches React !cols
			leaveWs.Column(1).Width = 12;
			leaveWs.Column(2).Width = 24;
			leaveWs.Column(3).Width = 30;
			leaveWs.Column(4).Width = 18;
			leaveWs.Column(5).Width = 20;
			for (int i = 0; i < leaveTypes.Count; i++)
				leaveWs.Column(leaveStartCol + i).Width = 16;
			leaveWs.Column(totalStartCol).Width = 10;
			leaveWs.Column(totalStartCol + 1).Width = 10;
			leaveWs.Column(totalStartCol + 2).Width = 10;

			// ════════════════════════════════════════════════════════════════
			// SHEET 2 — Timesheet (daily breakdown)
			// ════════════════════════════════════════════════════════════════
			var ts = wb.Worksheets.Add("Timesheet");

			// Build all dates in the month
			var allDates = new List<DateTime>();
			for (var d = new DateTime(monthStart.Year, monthStart.Month, 1);
				 d.Month == monthStart.Month;
				 d = d.AddDays(1))
				allDates.Add(d);

			// Row 1: fixed headers (single row, no merge — matches React)
			for (int i = 0; i < fixedHeaders.Length; i++)
			{
				var cell = ts.Cell(1, i + 1);
				cell.Value = fixedHeaders[i];
				StyleHeader(ts.Range(1, i + 1, 1, i + 1));
			}

			// Row 1 + 2: daily date columns — each date merges 3 cols in row 1,
			// row 2 has Regular | Overtime | Total sub-headers
			int col0 = 6; // 1-based start of date columns
			for (int i = 0; i < allDates.Count; i++)
			{
				int col = col0 + i * 3;
				var dateHeader = ts.Range(1, col, 1, col + 2).Merge();
				dateHeader.Value = allDates[i].ToString("dd-MM-yyyy");
				StyleHeader(dateHeader);

				ts.Cell(2, col).Value = "Regular";
				ts.Cell(2, col + 1).Value = "Overtime";
				ts.Cell(2, col + 2).Value = "Total";
				StyleHeader(ts.Range(2, col, 2, col + 2));
			}

			// TOTAL block in Timesheet sheet
			int tsTotalCol = col0 + allDates.Count * 3;
			int tsLastCol = tsTotalCol + 2;
			var tsTotalHeader = ts.Range(1, tsTotalCol, 1, tsLastCol).Merge();
			tsTotalHeader.Value = "TOTAL";
			StyleHeader(tsTotalHeader);

			ts.Cell(2, tsTotalCol).Value = "Regular";
			ts.Cell(2, tsTotalCol + 1).Value = "Overtime";
			ts.Cell(2, tsTotalCol + 2).Value = "Total";
			StyleHeader(ts.Range(2, tsTotalCol, 2, tsLastCol));

			// Data rows
			row = 3;
			var dailyReg = new decimal[allDates.Count];
			var dailyOt = new decimal[allDates.Count];
			var dailyTotal = new decimal[allDates.Count];
			decimal tGrandReg = 0, tGrandOt = 0, tGrandAll = 0;

			foreach (var emp in employees)
			{
				ts.Cell(row, 1).Value = emp.EmployeeId;
				ts.Cell(row, 2).Value = emp.EmployeeName;
				ts.Cell(row, 3).Value = emp.Username;
				ts.Cell(row, 4).Value = emp.Department;
				ts.Cell(row, 5).Value = emp.ProjectAssigned;

				for (int i = 0; i < allDates.Count; i++)
				{
					var entry = emp.Entries.FirstOrDefault(e => e.EntryDate.Date == allDates[i].Date);
					var dayTotal = entry?.WorkingHours ?? 0;
					var (regular, overtime) = SplitHours(dayTotal);
					int col = col0 + i * 3;

					SetHoursCell(ts.Cell(row, col), regular);
					SetHoursCell(ts.Cell(row, col + 1), overtime);
					SetHoursCell(ts.Cell(row, col + 2), dayTotal);

					dailyReg[i] += regular;
					dailyOt[i] += overtime;
					dailyTotal[i] += dayTotal;
				}

				SetHoursCell(ts.Cell(row, tsTotalCol), emp.TotalRegularHours);
				SetHoursCell(ts.Cell(row, tsTotalCol + 1), emp.TotalOvertimeHours);
				SetHoursCell(ts.Cell(row, tsTotalCol + 2), emp.TotalWorkingHours);

				tGrandReg += emp.TotalRegularHours;
				tGrandOt += emp.TotalOvertimeHours;
				tGrandAll += emp.TotalWorkingHours;
				row++;
			}

			// Daily total row — "DAILY TOTAL" label in col 5 (Project Assigned), matches React
			ts.Cell(row, 5).Value = "DAILY TOTAL";
			ts.Cell(row, 5).Style.Font.Bold = true;
			ts.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

			for (int i = 0; i < allDates.Count; i++)
			{
				int col = col0 + i * 3;
				SetHoursCell(ts.Cell(row, col), dailyReg[i]);
				SetHoursCell(ts.Cell(row, col + 1), dailyOt[i]);
				SetHoursCell(ts.Cell(row, col + 2), dailyTotal[i]);
				ts.Range(row, col, row, col + 2).Style.Font.Bold = true;
			}

			SetHoursCell(ts.Cell(row, tsTotalCol), tGrandReg);
			SetHoursCell(ts.Cell(row, tsTotalCol + 1), tGrandOt);
			SetHoursCell(ts.Cell(row, tsTotalCol + 2), tGrandAll);
			ts.Range(row, tsTotalCol, row, tsLastCol).Style.Font.Bold = true;

			// Column widths — matches React !cols
			ts.Column(1).Width = 12;
			ts.Column(2).Width = 24;
			ts.Column(3).Width = 30;
			ts.Column(4).Width = 18;
			ts.Column(5).Width = 20;
			for (int i = 0; i < allDates.Count; i++)
			{
				ts.Column(col0 + i * 3).Width = 10;
				ts.Column(col0 + i * 3 + 1).Width = 10;
				ts.Column(col0 + i * 3 + 2).Width = 10;
			}
			ts.Column(tsTotalCol).Width = 10;
			ts.Column(tsTotalCol + 1).Width = 10;
			ts.Column(tsTotalCol + 2).Width = 10;

			using var ms = new MemoryStream();
			wb.SaveAs(ms);
			return ms.ToArray();
		}
	}
}