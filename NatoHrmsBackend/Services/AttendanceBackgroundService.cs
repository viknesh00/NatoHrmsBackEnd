using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NatoHrmsBackend.Data;

public class AttendanceBackgroundService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AttendanceBackgroundService> _logger;

	public AttendanceBackgroundService(
		IServiceProvider serviceProvider,
		ILogger<AttendanceBackgroundService> logger)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Attendance Background Service Started");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();

				var context = scope.ServiceProvider
					.GetRequiredService<ApplicationDbContext>();

				await context.Database.ExecuteSqlRawAsync("EXEC FixMissingClockOutForAll");

				_logger.LogInformation("FixMissingClockOutForAll executed at {time}", DateTime.Now);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error running FixMissingClockOutForAll");
			}

			// Run every 4 hours
			await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
		}
	}
}