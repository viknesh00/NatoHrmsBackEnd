using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using NatoHrmsBackend.Models;

public interface IEmailService
{
	Task SendEmail(string to, string subject, string body, string cc = null, string bcc = null, EmailAttachment[]? attachments = null);
}

public class EmailService : IEmailService
{
	private readonly IConfiguration _configuration;

	public EmailService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public async Task SendEmail(
		string to,
		string subject,
		string body,
		string cc = null,
		string bcc = null,
		EmailAttachment[]? attachments = null
	)
	{
		var smtpHost = _configuration["Email:SmtpHost"];
		var smtpPort = int.Parse(_configuration["Email:SmtpPort"]);
		var smtpUser = _configuration["Email:SmtpUser"];
		var smtpPass = _configuration["Email:SmtpPass"];
		var fromEmail = _configuration["Email:FromEmail"];

		using (var client = new SmtpClient(smtpHost, smtpPort))
		{
			client.Credentials = new NetworkCredential(smtpUser, smtpPass);
			client.EnableSsl = true;

			using (var mail = new MailMessage())
			{
				mail.From = new MailAddress(fromEmail);
				mail.To.Add(to);

				if (!string.IsNullOrEmpty(cc)) mail.CC.Add(cc);
				if (!string.IsNullOrEmpty(bcc)) mail.Bcc.Add(bcc);

				mail.Subject = subject;
				mail.Body = body;
				mail.IsBodyHtml = true;

				// Add attachments if provided
				if (attachments != null)
				{
					foreach (var attachment in attachments)
					{
						var stream = new MemoryStream(attachment.Content);
						mail.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
					}
				}

				await client.SendMailAsync(mail);
			}
		}
	}
}

