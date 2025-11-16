using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using ESSLeaveSystem.BackgroundServices;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Configure Memory Cache
builder.Services.AddMemoryCache();

// Configure HTTP Client Factory
builder.Services.AddHttpClient();

// Configure SQLite database - INDEPENDENT from HR system
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=/tmp/essleave.db";

builder.Services.AddDbContext<LeaveDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure Identity with Cookie Authentication
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<LeaveDbContext>()
.AddDefaultTokenProviders();

// Configure Cookie Authentication for Razor Pages
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Register custom services
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();
builder.Services.AddScoped<IEmployeeLookupService, EmployeeLookupService>();
builder.Services.AddScoped<ILeaveBalanceInitializationService, LeaveBalanceInitializationService>();
builder.Services.AddScoped<ILeaveApprovalService, LeaveApprovalService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRoleAutoAssignmentService, RoleAutoAssignmentService>();
builder.Services.AddScoped<IEmployeeProfileService, EmployeeProfileService>();
builder.Services.AddSingleton<IPublicHolidayService, PublicHolidayService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// Register background service for auto-approval
builder.Services.AddHostedService<LeaveAutoApprovalBackgroundService>();

var app = builder.Build();

// Configure error handling based on environment
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTPS redirect only in production (not needed behind Railway/Render proxy)
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Root endpoint - prevent redirect loops
app.MapGet("/", (HttpContext context) =>
{
    // If already authenticated, go straight to dashboard
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/Index");
    }
    
    // Otherwise, redirect to login
    return Results.Redirect("/Account/Login");
}).AllowAnonymous();

// INDEPENDENT ESS LEAVE SYSTEM - Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    try
    {
        Console.WriteLine("ESS Leave System - Independent initialization starting...");
        Console.WriteLine($"   Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"   Connection: {connectionString}");

        var context      = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
        var userManager  = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager  = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure tables exist for ESS models
        var creator = context.Database.GetService<IRelationalDatabaseCreator>();
        try
        {
            creator.CreateTables();
            Console.WriteLine("ESS database tables created");
        }
        catch (Exception ex)
        {
            // This is usually "tables already exist", which is safe to ignore
            Console.WriteLine("ESS database tables already exist or could not be created:");
            Console.WriteLine($"   {ex.Message}");
        }

        // Ensure chatbot logs table
        EnsureChatbotLogsTable(context);

        // Seed complete ESS data (roles, departments, employees, users, leave types, balances)
        try
        {
            await ESSDataSeeder.SeedAsync(context, userManager, roleManager);
            Console.WriteLine("ESS Leave System fully initialized and ready!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Seeding] ESS data seeding failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ESS initialization fatal error: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

await app.RunAsync();

void EnsureChatbotLogsTable(LeaveDbContext context)
{
    try
    {
        var createTableSql = @"CREATE TABLE IF NOT EXISTS ChatbotQueryLogs (
            ChatbotQueryLogId INTEGER PRIMARY KEY AUTOINCREMENT,
            Email TEXT NULL,
            Question TEXT NOT NULL,
            Answer TEXT NOT NULL,
            CreatedDate TEXT NOT NULL
        );";

        context.Database.ExecuteSqlRaw(createTableSql);
        Console.WriteLine("Chatbot logs table ensured");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Chatbot Init] Failed to ensure ChatbotQueryLogs table: {ex.Message}");
    }
}
