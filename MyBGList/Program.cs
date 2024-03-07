using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBGList.Constants;
using MyBGList.Models;
using MyBGList.Swagger;
using Serilog;
using Serilog.Sinks.MSSqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders().AddSimpleConsole().AddDebug().AddApplicationInsights(telemetry => telemetry.ConnectionString
= builder.Configuration["Azure:ApplicationInsights:ConnectionString"],
    loggerOptions => { }
);

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration);
    lc.Enrich.WithMachineName();
    lc.Enrich.WithThreadId();
    lc.WriteTo.File("Logs/log.txt", outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}]"
        + "[{MachineName} #{ThreadId}]" +
        "{Message: lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day);

    lc.WriteTo.File("Logs/errors.txt", outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}]" +
        "[{MachineName} #{ThreadId} {ThreadName}]" + "{Message: lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error);

    lc.WriteTo.MSSqlServer(
        connectionString: ctx.Configuration.GetConnectionString("DefaultConnection"),
        sinkOptions: new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions { TableName = "LogEvents", AutoCreateSqlTable = true },
        columnOptions : new Serilog.Sinks.MSSqlServer.ColumnOptions()
        {
            AdditionalColumns = new SqlColumn[]
            {
                new SqlColumn()
                {
                    ColumnName = "SourceContext",
                    PropertyName = "SourceContext",
                    DataType = System.Data.SqlDbType.NVarChar
                }
            }
        }
        );
}, writeToProviders: true);
// Add services to the container.

builder.Services.AddControllers(options => { options.ModelBindingMessageProvider.SetValueIsInvalidAccessor((x) => $"The value '{x}' is invalid.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(x => $"The field {x} must be a number.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((x, y) => $"The value '{x}' is not valid for '{y}'.");
    options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(() => $"A value is required");
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.ParameterFilter<SortColumnFilter>();
    options.ParameterFilter<SortOrderFilter>();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(cfg =>
    {
        cfg.WithOrigins(builder.Configuration["AllowedOrigins"]!);
        cfg.AllowAnyHeader();
        cfg.AllowAnyMethod();
    });
    options.AddPolicy(name: "AnyOrigin", cfg =>
    {
        cfg.AllowAnyOrigin();
        cfg.AllowAnyHeader();
        cfg.AllowAnyMethod();
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("UseDeveloperExceptionPage"))
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/error");

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapGet("/error",
    [EnableCors("AnyOrigin")]
[ResponseCache(NoStore = true)] (HttpContext context) =>
    {
        var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();
        var details = new ProblemDetails();
        details.Detail = exceptionHandler?.Error.Message;
        details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        details.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
        details.Status = StatusCodes.Status500InternalServerError;

        app.Logger.LogError(CustomLogEvents.Error_Get, exceptionHandler?.Error, "An unhandled exception occured.");

        return Results.Problem(details);
    });
app.MapGet("/error/test", [EnableCors("AnyOrigin")][ResponseCache(NoStore = true)] () => { throw new Exception("test"); });


app.MapControllers();

app.Run();
