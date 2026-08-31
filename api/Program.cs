using Amanah.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UsePipeline();

app.Run();
