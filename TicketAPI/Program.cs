using Microsoft.EntityFrameworkCore;
using TicketAPI;

var builder = WebApplication.CreateBuilder(args);

// Kết nối SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// API 1: Lấy danh sách sự kiện và số vé còn lại
app.MapGet("/api/events", async (AppDbContext db) =>
{
    var events = await db.Events.ToListAsync();
    return Results.Ok(events);
});

app.Run();