using InquiryService.Api.Middlewares;
using InquiryService.Application.Contracts.Caching;
using InquiryService.Application.Contracts.Infrastructure;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Application.Services;
using InquiryService.Infrastructure.Caching;
using InquiryService.Infrastructure.Data;
using InquiryService.Infrastructure.Data.Repositories;
using InquiryService.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddScoped<IInquiryOrchestrator, InquiryOrchestrator>();

builder.Services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IInquiryRepository, InquiryRepository>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IInquiryCacheService, MemoryInquiryCacheService>();

builder.Services.AddScoped<IInquiryProvider, PrimaryMockProvider>();
builder.Services.AddScoped<IInquiryProvider, SecondaryMockProvider>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Inquiry API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();