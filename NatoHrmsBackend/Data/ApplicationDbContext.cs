using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Models;

namespace NatoHrmsBackend.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
			: base(options) { }

		public DbSet<User> Users { get; set; }
		public DbSet<UserLogin> UserLogins { get; set; }
		public DbSet<UserList> UserLists { get; set; }
		public DbSet<SalaryDetail> SalaryDetails { get; set; }
		public DbSet<Announcement> Announcements { get; set; }
		public DbSet<AnnouncementList>AnnouncementLists { get; set; }
		public DbSet<UserLeaveRequest>UserLeaveRequests { get; set; }
		public DbSet<Attendance> Attendance { get; set; }
		public DbSet<AttendanceResponse> AttendanceResponses { get; set; }
		public DbSet<AttendanceRequest> AttendanceRequests { get; set; }
		public DbSet<AttendanceMonthlyResponse> AttendanceMonthlyResponses { get; set; }
		public DbSet<AttendanceMonthlyRequest> AttendanceMonthlyRequests { get; set; }
		public DbSet<DepartmentTiming> DepartmentTimings { get; set; }
		public DbSet<DepartmentTimingResponse> DepartmentTimingResponses { get; set; }
		public DbSet<TimeSheetResponse>TimeSheetResponses { get; set; }
		public DbSet<EmailCheckResponses>EmailCheckResponses { get; set; }
		public DbSet<DropDownItem> DropDownItems { get; set; }
		public DbSet<HolidayResponse> HolidayResponses { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<UserList>().HasNoKey();
			modelBuilder.Entity<SalaryDetail>().HasNoKey();
			modelBuilder.Entity<Announcement>().HasNoKey();
			modelBuilder.Entity<AnnouncementList>().HasNoKey();
			modelBuilder.Entity<UserLeaveRequest>().HasNoKey();
			modelBuilder.Entity<Attendance>().HasNoKey();
			modelBuilder.Entity<AttendanceResponse>().HasNoKey();
			modelBuilder.Entity<AttendanceRequest>().HasNoKey();
			modelBuilder.Entity<AttendanceMonthlyResponse>().HasNoKey();
			modelBuilder.Entity<AttendanceMonthlyRequest>().HasNoKey();
			modelBuilder.Entity<DepartmentTiming>().HasNoKey();
			modelBuilder.Entity<DepartmentTimingResponse>().HasNoKey();
			modelBuilder.Entity<TimeSheetResponse>().HasNoKey();
			modelBuilder.Entity<EmailCheckResponses>().HasNoKey();
			modelBuilder.Entity<DropDownItem>().HasNoKey();
			modelBuilder.Entity<HolidayResponse>().HasNoKey();

			modelBuilder.Entity<User>()
				.HasKey(u => u.UserId);

			modelBuilder.Entity<UserLogin>()
				.HasOne(ul => ul.User)
				.WithMany()
				.HasForeignKey(ul => ul.UserId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
