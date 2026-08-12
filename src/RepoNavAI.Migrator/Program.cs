using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Pgvector.EntityFrameworkCore;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is required.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    connectionString,
    postgres => ConfigurePostgres(postgres)));
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

using var host = builder.Build();
await host.Services.InitializeDatabaseAsync();

static void ConfigurePostgres(NpgsqlDbContextOptionsBuilder postgres)
{
    postgres.MigrationsHistoryTable("__EFMigrationsHistory", "reponav");
    postgres.UseVector();
}
