using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatchProj.WebUI.Components;
using PatchProj.WebUI.Utils;

namespace PatchProj.WebUI
{
	public class Program
	{
		public static void Main(string[] args)
		{
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services
				.AddRazorComponents()
				.AddInteractiveServerComponents();
			Config config = builder.Configuration.GetSection("Config").Get<Config>()!;
			builder.Services.AddSingleton(config);
			builder.Services.AddSingleton<Class>();
			builder.Services.AddScoped<ScopedUser>();
			WebApplication app = builder.Build();

			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
			}

			app.UseStaticFiles();
			app.UseAntiforgery();

			app.MapRazorComponents<App>()
				.AddInteractiveServerRenderMode();

			app.Run();
		}
	}
}
