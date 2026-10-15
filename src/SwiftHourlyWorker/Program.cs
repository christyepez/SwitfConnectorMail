using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwiftHourlyWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<SqlReportRepository>();
builder.Services.AddSingleton<OutlookDesktopMailSender>();
builder.Services.AddSingleton<CheckpointStore>();
builder.Services.AddHostedService<HourlyWorker>();
await builder.Build().RunAsync();
