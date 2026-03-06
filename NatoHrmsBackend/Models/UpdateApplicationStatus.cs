using Microsoft.AspNetCore.Routing.Matching;
using System;

namespace NatoHrmsBackend.Models
{
	public class UpdateApplicationStatus
	{
		public int ApplicationId { get; set; }
		public string CandidateStatus { get; set; }
		public string AssignedTo { get; set; }
	}
}
