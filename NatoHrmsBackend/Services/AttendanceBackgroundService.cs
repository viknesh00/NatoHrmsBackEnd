using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using NatoHrmsBackend.Data;

public class AttendanceBackgroundService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;

	public AttendanceBackgroundService(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var context = scope.ServiceProvider
					.GetRequiredService<ApplicationDbContext>();

				await context.Database.ExecuteSqlRawAsync(
					"EXEC FixMissingClockOutForAll",
					stoppingToken
				);
			}

			await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
		}
	}
}