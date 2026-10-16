namespace NatoHrmsBackend.Models
{
	public class EmployeeDetailsRequest
	{
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string DateOfBirth { get; set; }        // DD/MM/YYYY
		public string PlaceOfBirth { get; set; }
		public string MobileNumber { get; set; }       // with country code, e.g. +91 98765 43210
		public string PassportNo { get; set; }
		public string? SALCode { get; set; }
		public string City { get; set; }
		public string Country { get; set; }
		public string CurrentAddress { get; set; }
		public string? NConsEmail { get; set; }
		public string? NConsEmailStatus { get; set; }   // Enabled / Disabled
		public string PersonalEmail { get; set; }

		public IFormFile PassportCopy { get; set; }    // photo + passport number page (JPG / PNG / PDF)
		public IFormFile PassportPhoto { get; set; }   // passport-size photo (JPG / PNG / PDF)
	}
}
