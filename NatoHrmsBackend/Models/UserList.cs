namespace NatoHrmsBackend.Models
{
	public class UserList
	{
		public int UserId { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? Gender { get; set; }
		public DateTime? DOB { get; set; }
		public string? MaritalStatus { get; set; }
		public string? Nationality { get; set; }
		public string? BloodGroup { get; set; }
		public string? ContactNumber { get; set; }
		public string? Email { get; set; }
		public string? Address { get; set; }
		public string? EmployeeType { get; set; }
		public string? Department { get; set; }
		public string? Designation { get; set; }
		public DateTime? DOJ { get; set; }
		public string? WorkLocation { get; set; }
		public string? ReportingManager { get; set; }
		public string? AccessRole { get; set; }
		public string? EmploymentStatus { get; set; }
		public string? EmployeeId { get; set; }
		public decimal? CTC { get; set; }
		public decimal? BasicSalary { get; set; }
		public decimal? HRA { get; set; }
		public decimal? EmployeePF { get; set; }
		public string? PFAccountNumber { get; set; }
		public decimal? MedicalAllowance { get; set; }
		public decimal? ConveyanceAllowance { get; set; }
		public string? ESINumber { get; set; }
		public decimal? SpecialAllowance { get; set; }
		public string? BankName { get; set; }
		public string? AccountNumber { get; set; }
		public string? IFSCCode { get; set; }
		public string? PanNumber { get; set; }
		public string? UANNumber { get; set; }
		public string? HighestQualification { get; set; }
		public string? Specialization { get; set; }
		public string? University { get; set; }
		public string? YearOfPassing { get; set; }
		public string? PreviousCompany { get; set; }
		public string? TotalExperience { get; set; }
		public string? EmergencyContactName { get; set; }
		public string? EmergencyContactNumber { get; set; }
		public string? Relationship { get; set; }
		public string? WorkShift { get; set; }
		public string? WorkMode { get; set; }
		public string? Notes { get; set; }
		public string? ProfilePhoto { get; set; }
		public string? Resume { get; set; }
		public string? AadharCard { get; set; }
		public string? PanCard { get; set; }
		public string? OfferLetter { get; set; }
		public DateTime? CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }

		// 🔥 Added from UserLogins table
		public bool? IsActive { get; set; }
		public DateTime? LastLoginAt { get; set; }
	}
}
