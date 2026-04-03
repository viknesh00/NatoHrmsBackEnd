using System.Linq;

namespace NatoHrmsBackend.Services
{
	public static class EmailTemplates
	{
		// ─── BASE WRAPPER ────────────────────────────────────────────────────────
		private static string Base(string content) => $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1.0""/>
<title>Natobotics HRMS</title>
<link href=""https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700;800;900&family=DM+Sans:wght@400;500;600;700&display=swap"" rel=""stylesheet""/>
</head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:'DM Sans',Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f1f5f9;padding:32px 16px;"">
<tr><td align=""center"">
<table role=""presentation"" width=""580"" cellpadding=""0"" cellspacing=""0"" style=""width:580px;max-width:580px;"">

  <!-- Logo Header -->
  <tr>
    <td align=""center"" style=""padding-bottom:20px;"">
      <img src=""https://www.natobotics.com/img/Natobotics.png"" alt=""Natobotics"" width=""50"" style=""display:block;border-radius:10px;""/>
      <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:11px;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;color:#64748b;margin-top:6px;"">NATOBOTICS HRMS</div>
    </td>
  </tr>

  <!-- Main card -->
  <tr>
    <td style=""background:#ffffff;border-radius:16px;overflow:hidden;border:1.5px solid #e2e8f0;"">
      {content}
    </td>
  </tr>

  <!-- Footer -->
  <tr>
    <td style=""padding:24px 0 8px;text-align:center;"">
      <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:12px;color:#94a3b8;line-height:1.7;"">
        <strong style=""color:#64748b;"">Natobotics Technologies Pvt Ltd</strong><br/>
        Tidel Park, Taramani, Chennai – 600113<br/>
        <a href=""mailto:hr@natobotics.com"" style=""color:#6c3fc5;text-decoration:none;"">hr@natobotics.com</a>
        &nbsp;|&nbsp;
        <a href=""https://www.natobotics.com"" style=""color:#6c3fc5;text-decoration:none;"">www.natobotics.com</a>
      </div>
      <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:12px;color:#64748b;margin-top:10px;font-style:italic;"">
        This is an automated email from the HRMS system. Please do not reply directly.
      </div>
    </td>
  </tr>

</table>
</td></tr>
</table>
</body>
</html>";

		// ─── SHARED COMPONENTS ───────────────────────────────────────────────────
		static string TopBar(string color = "#6c3fc5") =>
			$@"<tr><td style=""height:5px;background:linear-gradient(90deg,{color},#0d9488);""></td></tr>";

		static string InfoRow(string label, string value) =>
			$@"<tr>
              <td style=""padding:8px 0;border-bottom:1px solid #f1f5f9;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td style=""font-family:'DM Sans',Arial,sans-serif;font-size:12px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;width:140px;"">{label}</td>
                    <td style=""font-family:'DM Sans',Arial,sans-serif;font-size:13px;font-weight:600;color:#1e293b;"">{value}</td>
                  </tr>
                </table>
              </td>
            </tr>";

		static string InfoTable(string rows) =>
			$@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""
               style=""background:#f8f7ff;border-radius:10px;padding:14px 18px;margin:16px 0;border:1.5px solid #ede9fe;"">
              {rows}
            </table>";

		static string Badge(string label, string bg, string color) =>
			$@"<span style=""font-family:'DM Sans',Arial,sans-serif;display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:{bg};color:{color};"">
              {label}
            </span>";

		static string AlertBox(string icon, string text) =>
			$@"<tr>
              <td style=""padding:10px 0;"">
                <div style=""border-left:4px solid #f59e0b;background:#fffbeb;padding:12px 16px;border-radius:0 8px 8px 0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#78350f;"">
                  {icon} {text}
                </div>
              </td>
            </tr>";

		static string BodyPad(string innerContent) =>
			$@"<tr><td style=""padding:32px 36px;"">{innerContent}</td></tr>";

		static string Eyebrow(string text, string color) =>
			$@"<p style=""margin:0 0 6px;font-family:'DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:{color};"">{text}</p>";

		static string H1(string text) =>
			$@"<h1 style=""margin:0 0 8px;font-family:'Playfair Display',Georgia,serif;font-size:26px;font-weight:800;color:#1e1143;line-height:1.25;"">{text}</h1>";

		// ─── OTP DIGIT SPACER ────────────────────────────────────────────────────
		// Adds margin-right between digits only — no trailing space on the last digit,
		// so the OTP block is truly centered without letter-spacing bleed.
		static string SpacedOtp(string otp) =>
			string.Concat(otp.Select((c, i) =>
				i < otp.Length - 1
					? $@"<span style=""margin-right:10px;"">{c}</span>"
					: $@"<span>{c}</span>"
			));

		// ─── OTP EMAIL ───────────────────────────────────────────────────────────
		public static string OtpEmail(string firstName, string lastName, string otp) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar()}
              {BodyPad($@"
                {Eyebrow("Password Reset", "#6c3fc5")}
                {H1("Secure Verification")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">
                  Dear <strong style=""color:#1e1143;"">{firstName} {lastName}</strong>,<br/>
                  You requested a password reset for your Natobotics HRMS account.<br/>
                  Use the One-Time Password below to continue:
                </p>

                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td align=""center"" style=""padding:6px 0 20px;"">
                      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""border:1.5px solid #c4b5f4;border-radius:14px;background:#faf8ff;"">
                        <tr>
                          <td align=""center"" style=""padding:24px 40px;"">
                            <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:11px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;color:#6c3fc5;margin-bottom:10px;"">YOUR OTP CODE</div>
                            <div style=""font-family:'Playfair Display',Georgia,serif;font-size:44px;font-weight:900;color:#6c3fc5;line-height:1;letter-spacing:0;"">
                              {SpacedOtp(otp)}
                            </div>
                            <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:12px;color:#64748b;margin-top:10px;"">⏱️ Valid for <strong>10 minutes</strong>. Do not share this code.</div>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>

                <div style=""border-left:4px solid #f59e0b;background:#fffbeb;padding:12px 16px;border-radius:0 8px 8px 0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#78350f;margin-bottom:20px;"">
                  🔒 If you did not request this password reset, please ignore this email or contact your administrator immediately.
                </div>
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">Regards,<br/><strong style=""color:#1e1143;"">Natobotics HRMS Team</strong></p>
              ")}
            </table>
            ");

		// ─── WELCOME EMAIL ───────────────────────────────────────────────────────
		public static string WelcomeEmail(string firstName, string lastName, string email, string defaultPassword, string employeeId) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar("#0d9488")}
              {BodyPad($@"
                {Eyebrow("Welcome Aboard 🎉", "#0d9488")}
                {H1($"Hello, {firstName} {lastName}!")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">
                  Your Natobotics HRMS account has been created successfully. Here are your login credentials to get started:
                </p>

                {InfoTable($@"
                  {InfoRow("Employee ID", $@"<strong>{employeeId}</strong>")}
                  {InfoRow("Login Email", email)}
                  {InfoRow("Temp Password", $@"<strong style='color:#6c3fc5;font-family:monospace;font-size:14px;'>{defaultPassword}</strong>")}
                  {InfoRow("Portal URL", $@"<a href='https://natoboticshrms.vercel.app/' style='color:#6c3fc5;font-family:DM Sans,Arial,sans-serif;'>natoboticshrms</a>")}
                ")}

                <div style=""border-left:4px solid #f59e0b;background:#fffbeb;padding:12px 16px;border-radius:0 8px 8px 0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#78350f;margin-bottom:20px;"">
                  🔑 You will be prompted to change your password on first login. Keep your credentials confidential.
                </div>
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">We're excited to have you on board!<br/><strong style=""color:#1e1143;"">Natobotics HR Team</strong></p>
              ")}
            </table>
            ");

		// ─── LEAVE APPROVAL ──────────────────────────────────────────────────────
		public static string LeaveApprovalEmail(string employeeName, string leaveType, string fromDate, string toDate, bool isApproved, string approverReason, string approverName) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar(isApproved ? "#0d9488" : "#ef4444")}
              {BodyPad($@"
                {Eyebrow("Leave Request Update", isApproved ? "#0d9488" : "#ef4444")}
                {H1(isApproved ? "Leave Approved ✅" : "Leave Declined ❌")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">Dear <strong style=""color:#1e1143;"">{employeeName}</strong>,<br/>Your leave request has been reviewed and {(isApproved ? "approved" : "declined")} by your manager.</p>

                {InfoTable($@"
                  {InfoRow("Leave Type", leaveType)}
                  {InfoRow("From Date", fromDate)}
                  {InfoRow("To Date", toDate)}
                  {InfoRow("Reviewed By", approverName)}
                  {InfoRow("Decision", isApproved
					? "<span style='font-family:DM Sans,Arial,sans-serif;display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:#dcfce7;color:#15803d;'>Approved</span>"
					: "<span style='font-family:DM Sans,Arial,sans-serif;display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:#fee2e2;color:#b91c1c;'>Declined</span>")}
                  {(!isApproved && !string.IsNullOrEmpty(approverReason) ? InfoRow("Reason", approverReason) : "")}
                ")}

                {(isApproved
				  ? @"<p style=""font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;margin:0 0 20px;"">Please ensure your tasks are handed over before your leave begins. Enjoy your time off! 🌴</p>"
				  : @"<p style=""font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;margin:0 0 20px;"">If you have concerns, please speak with your manager or contact the HR team.</p>"
				)}
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">Regards,<br/><strong style=""color:#1e1143;"">Natobotics HRMS Team</strong></p>
              ")}
            </table>
            ");

		// ─── LEAVE REQUEST NOTIFICATION (TO MANAGER) ─────────────────────────────
		public static string LeaveRequestNotificationEmail(string managerName, string employeeName, string leaveType, string fromDate, string toDate, string reason) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar("#f59e0b")}
              {BodyPad($@"
                {Eyebrow("Action Required", "#b45309")}
                {H1("New Leave Request")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">Dear <strong style=""color:#1e1143;"">{managerName}</strong>,<br/>A new leave request has been submitted and requires your attention.</p>

                {InfoTable($@"
                  {InfoRow("Employee", employeeName)}
                  {InfoRow("Leave Type", leaveType)}
                  {InfoRow("From Date", fromDate)}
                  {InfoRow("To Date", toDate)}
                  {InfoRow("Reason", reason)}
                  {InfoRow("Status", "<span style='font-family:DM Sans,Arial,sans-serif;display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:#fef3c7;color:#b45309;'>Pending Review</span>")}
                ")}

                <p style=""font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;margin:0 0 20px;"">Please log in to the HRMS portal to review and take action.</p>
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">Regards,<br/><strong style=""color:#1e1143;"">Natobotics HRMS System</strong></p>
              ")}
            </table>
            ");

		// ─── ANNOUNCEMENT ────────────────────────────────────────────────────────
		public static string AnnouncementEmail(string recipientName, string description, string department, string announcementDate) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar("#8b5cf6")}
              {BodyPad($@"
                {Eyebrow("New Announcement 📢", "#6c3fc5")}
                {H1("Company Announcement")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">Dear <strong style=""color:#1e1143;"">{recipientName}</strong>, a new announcement has been published:</p>

                {InfoTable($@"
                  {InfoRow("Date", announcementDate)}
                  {InfoRow("Department", department)}
                ")}

                <div style=""background:#f0f4ff;border:1.5px solid #c7d2fe;border-radius:10px;padding:18px 20px;margin:16px 0;"">
                  <div style=""font-family:'DM Sans',Arial,sans-serif;font-size:11px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:#6c3fc5;margin-bottom:8px;"">📌 Announcement</div>
                  <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#1e293b;line-height:1.7;"">{description}</p>
                </div>

                <p style=""font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;margin:0 0 20px;"">Log in to the HRMS portal for more details.</p>
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">Regards,<br/><strong style=""color:#1e1143;"">Natobotics HR Team</strong></p>
              ")}
            </table>
            ");

		// ─── PASSWORD CHANGED ────────────────────────────────────────────────────
		public static string PasswordChangedEmail(string firstName, string lastName) =>
			Base($@"
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {TopBar("#0d9488")}
              {BodyPad($@"
                {Eyebrow("Security Alert", "#0d9488")}
                {H1("Password Changed ✅")}
                <p style=""margin:0 0 20px;font-family:'DM Sans',Arial,sans-serif;font-size:14px;color:#475569;line-height:1.7;"">Hi <strong style=""color:#1e1143;"">{firstName} {lastName}</strong>,<br/>Your HRMS account password was successfully changed.</p>

                {InfoTable($@"
                  {InfoRow("Account", $"{firstName} {lastName}")}
                  {InfoRow("Changed On", DateTime.Now.ToString("dd MMM yyyy, hh:mm tt"))}
                  {InfoRow("Status", "<span style='font-family:DM Sans,Arial,sans-serif;display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:#dcfce7;color:#15803d;'>Successful</span>")}
                ")}

                <div style=""border-left:4px solid #f59e0b;background:#fffbeb;padding:12px 16px;border-radius:0 8px 8px 0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#78350f;margin-bottom:20px;"">
                  🔒 If you did not make this change, contact your administrator or HR immediately to secure your account.
                </div>
                <p style=""margin:0;font-family:'DM Sans',Arial,sans-serif;font-size:13px;color:#475569;"">Regards,<br/><strong style=""color:#1e1143;"">Natobotics HRMS Security Team</strong></p>
              ")}
            </table>
            ");
	}
}