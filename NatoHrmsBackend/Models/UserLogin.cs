using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NatoHrmsBackend.Models
{
	public class UserLogin
	{
		[Key]
		public int LoginId { get; set; }

		[ForeignKey("User")]
		public int UserId { get; set; }

		[Required]
		public string UserName { get; set; }  // usually Email or EmployeeId

		[Required]
		public string PasswordHash { get; set; }

		public bool IsActive { get; set; } = true;
		public bool IsDefaultPasswordChanged { get; set; } = false;
		public DateTime? LastLoginAt { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		public virtual User User { get; set; }
	}
}
