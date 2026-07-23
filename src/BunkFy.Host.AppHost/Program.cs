using BunkFy.AppHost.Composition;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
builder.AddBunkFyBackend(new(
    Api: "../BunkFy.Host.Api/BunkFy.Host.Api.csproj",
    AdminApi: "../BunkFy.Host.AdminApi/BunkFy.Host.AdminApi.csproj",
    Worker: "../BunkFy.Host.Worker/BunkFy.Host.Worker.csproj",
    Migrations: "../BunkFy.Host.Migrations/BunkFy.Host.Migrations.csproj"));
builder.Build().Run();
