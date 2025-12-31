using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using TR.Connector.ServiceMockup.Authentication;
using TR.Connector.ServiceMockup.Services;

namespace TR.Connector.ServiceMockup
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.

			builder.Services.AddControllers();
			builder.Services.AddAuthentication("dummy").AddScheme<AuthenticationSchemeOptions, DummyAuthenticationHandler>("dummy", options => { });

			builder.Services.AddSingleton<DummyDataStore>();

			builder.Services.AddHttpLogging(opts =>
			{
				opts.RequestHeaders.Add("Authorization");
			});

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			
			app.UseAuthorization();

			app.UseHttpLogging();

			app.MapControllers();

			app.Run();
		}
	}
}
