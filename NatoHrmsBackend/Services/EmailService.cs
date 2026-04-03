using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using NatoHrmsBackend.Models;

public interface IEmailService
{
	Task SendEmail(string to, string subject, string body, string cc = null, string bcc = null, EmailAttachment[]? attachments = null, bool includeCcFromConfig = false);
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
		EmailAttachment[]? attachments = null,
		bool includeCcFromConfig = false
	)
	{
		var smtpHost  = _configuration["Email:SmtpHost"];
		var smtpPort  = int.Parse(_configuration["Email:SmtpPort"]);
		var smtpUser  = _configuration["Email:SmtpUser"];
		var smtpPass  = _configuration["Email:SmtpPass"];
		var fromEmail = _configuration["Email:FromEmail"];

		using var client = new SmtpClient(smtpHost, smtpPort)
		{
			Credentials = new NetworkCredential(smtpUser, smtpPass),
			EnableSsl   = true
		};

		using var mail = new MailMessage();
		mail.From       = new MailAddress(fromEmail);
		mail.Subject    = subject;
		mail.Body       = body;
		mail.IsBodyHtml = true;

		// Primary recipient
		mail.To.Add(to);

		// Explicit CC passed by caller
		if (!string.IsNullOrWhiteSpace(cc))
		{
			foreach (var addr in cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				mail.CC.Add(addr);
		}

		// CC from appsettings (opt-in per call, skipped for user-creation & password emails)
		if (includeCcFromConfig)
		{
			var configCc = _configuration["Email:CcEmails"];
			if (!string.IsNullOrWhiteSpace(configCc))
			{
				foreach (var addr in configCc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
					mail.CC.Add(addr);
			}
		}

		if (!string.IsNullOrWhiteSpace(bcc))
		{
			foreach (var addr in bcc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				mail.Bcc.Add(addr);
		}

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
