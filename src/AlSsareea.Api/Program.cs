using AlSsareea.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiServices(builder.Configuration);

WebApplication app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();

app.Run();

public partial class Program;
