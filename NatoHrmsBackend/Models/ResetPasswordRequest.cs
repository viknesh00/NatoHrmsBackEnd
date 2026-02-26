namespace NatoHrmsBackend.Models
{
	public class ResetPasswordRequest
	{
		public string Email { get; set; }
		public string Otp { get; set; }
		public string NewPassword { get; set; }
	}
}
