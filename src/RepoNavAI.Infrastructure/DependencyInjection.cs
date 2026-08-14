using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RepoNavAI.Application.Authentication;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Infrastructure.Authentication;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Infrastructure.Persistence;
using RepoNavAI.Infrastructure.Organizations;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Infrastructure.Repositories;
using Pgvector.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace RepoNavAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is required.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            connectionString,
            postgres => { postgres.MigrationsHistoryTable("__EFMigrationsHistory", "reponav"); postgres.UseVector(); }));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateDataAnnotations().Validate(x => x.SigningKey.Length >= 32, "JWT signing key must be at least 32 characters.").ValidateOnStart();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });
        services.AddAuthorization();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddSingleton<IInvitationTokenService, InvitationTokenService>();
        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.SectionName));
        services.AddHttpClient<IRepositoryProvider, GitHubRepositoryProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoNavAI/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IRepositoryRegistrationRepository, RepositoryRegistrationRepository>();
        services.AddScoped<IRepositoryRemovalStore, RepositoryRemovalStore>();
        services.AddScoped<IRepositoryQueries, RepositoryQueries>();
        services.AddScoped<IRepositoryFavoriteStore, RepositoryFavoriteStore>();
        services.AddOptions<IndexingOptions>().Bind(configuration.GetSection(IndexingOptions.SectionName))
            .Validate(x => x.PollSeconds is >= 1 and <= 60, "Indexing poll interval must be between 1 and 60 seconds.")
            .Validate(x => x.LeaseSeconds is >= 15 and <= 300, "Indexing lease must be between 15 and 300 seconds.")
            .Validate(x => x.HeartbeatSeconds >= 1 && x.HeartbeatSeconds * 3 <= x.LeaseSeconds, "Indexing heartbeat must run at least three times per lease.")
            .ValidateOnStart();
        services.AddScoped<IndexingQueueStore>();
        services.AddScoped<IIndexingRequestRepository>(provider => provider.GetRequiredService<IndexingQueueStore>());
        services.AddSingleton<ISourceSymbolParser, CSharpSourceSymbolParser>();
        services.AddSingleton<IRepositoryEndpointAnalyzer, AspNetEndpointAnalyzer>();
        services.AddSingleton<ISourceChunker, SourceChunker>();
        services.AddOptions<OpenAIOptions>().Bind(configuration.GetSection(OpenAIOptions.SectionName)).Validate(x => x.EmbeddingDimensions == 512, "OpenAI embedding dimensions must match the configured pgvector(512) schema.").Validate(x => !string.IsNullOrWhiteSpace(x.EmbeddingModel), "An embedding model is required.").Validate(x => x.ChatMaxOutputTokens is >= 256 and <= 4096, "Chat output tokens must be between 256 and 4096.").Validate(x => x.CodeFlowMaxOutputTokens is >= 512 and <= 4096, "Code-flow output tokens must be between 512 and 4096.").Validate(x => x.CodeFlowTimeoutSeconds is >= 10 and <= 300, "Code-flow timeout must be between 10 and 300 seconds.").Validate(x => x.ChatMaximumContextCharacters is >= 8_000 and <= 100_000, "Chat context size is outside the supported range.").ValidateOnStart();
        services.AddHttpClient<IEmbeddingGenerator, OpenAIEmbeddingGenerator>(client => { client.BaseAddress = new Uri("https://api.openai.com/v1/"); client.Timeout = TimeSpan.FromMinutes(2); });
        var openAI = configuration.GetSection(OpenAIOptions.SectionName).Get<OpenAIOptions>() ?? new OpenAIOptions();
        if (!string.IsNullOrWhiteSpace(openAI.ApiKey))
        {
            services.AddOpenAIChatCompletion(openAI.ChatModel, openAI.ApiKey);
            services.AddScoped<IRepositoryAnswerGenerator, SemanticKernelRepositoryAnswerGenerator>();
            services.AddScoped<IRepositoryOrientationGenerator, SemanticKernelRepositoryOrientationGenerator>();
            services.AddScoped<IRepositoryCodeFlowGenerator, SemanticKernelRepositoryCodeFlowGenerator>();
        }
        else
        {
            services.AddSingleton<IRepositoryAnswerGenerator, UnavailableRepositoryAnswerGenerator>();
            services.AddSingleton<IRepositoryOrientationGenerator, UnavailableRepositoryOrientationGenerator>();
            services.AddSingleton<IRepositoryCodeFlowGenerator, UnavailableRepositoryCodeFlowGenerator>();
        }
        services.AddOptions<RepositoryChatOptions>().Bind(configuration.GetSection(RepositoryChatOptions.SectionName)).Validate(x => x.OrganizationDailyRequestLimit is >= 1 and <= 10_000, "Repository chat daily limit is outside the supported range.").ValidateOnStart();
        services.AddScoped<IRepositoryChatSessionStore, RepositoryChatSessionStore>();
        services.AddScoped<IRepositoryOrientationStore, RepositoryOrientationStore>();
        services.AddScoped<IVectorStore, PgVectorStore>();
        services.AddHttpClient<IRepositorySnapshotProvider, GitHubSnapshotProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/"); client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoNavAI/1.0"); client.Timeout = TimeSpan.FromMinutes(2);
        });
        return services;
    }

    public static IServiceCollection AddRepositoryIndexingWorker(this IServiceCollection services)
    {
        services.AddHostedService<RepositoryIndexingWorker>();
        return services;
    }
}
