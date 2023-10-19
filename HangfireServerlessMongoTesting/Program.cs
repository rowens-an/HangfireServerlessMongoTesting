using Hangfire;
using Hangfire.Console;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using MongoDB.Driver;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var mongoUrlBuilder = new MongoUrlBuilder(builder.Configuration["MongoDb:ConnectionString"]);
var databaseName = builder.Configuration["MongoDb:DatabaseName"];
var mongoClient = new MongoClient(mongoUrlBuilder.ToMongoUrl());

GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute {Attempts = 0});

var opts = new MongoStorageOptions
{
    MigrationOptions = new MongoMigrationOptions
    {
        MigrationStrategy = new MigrateMongoMigrationStrategy(),
        BackupStrategy = new CollectionMongoBackupStrategy(),
                
    },
    Prefix = "test.hangfire",
    CheckConnection = true,
    CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.Poll,
    SupportsCappedCollection = false
};

builder.Services.AddHangfire(c =>
{
    c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseConsole()
        .UseMongoStorage(mongoClient, databaseName, opts);
    c.UseSerializerSettings(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
});

builder.Services.AddHangfireServer(s => { s.ServerName = "test"; });





var app = builder.Build();

app.UseRouting();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecks("/health");
    var hangfireUser = app.Configuration["Hangfire:User"];
    var hangfirePassword = app.Configuration["Hangfire:Password"];

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[]
        {
            new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
            {
                RequireSsl = false,
                SslRedirect = false,
                LoginCaseSensitive = true,
                Users = new[]
                {
                    new BasicAuthAuthorizationUser
                    {
                        Login = hangfireUser,
                        PasswordClear = hangfirePassword
                    }
                }
            })
        }
    });
});


RecurringJob.AddOrUpdate(
    "myrecurringjob",
    () => Console.WriteLine("Recurring!"),
    Cron.Minutely);

app.Run();