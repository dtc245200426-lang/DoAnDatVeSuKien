using Microsoft.EntityFrameworkCore;
using TicketAPI;

var builder = WebApplication.CreateBuilder(args);

// Kết nối SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// 1. Xem danh sách sự kiện và số vé còn lại
app.MapGet("/api/events", async (AppDbContext db) =>
{
    var events = await db.Events.ToListAsync();
    return Results.Ok(events);
});

// 2. Gửi yêu cầu đặt vé mới (Trạng thái ban đầu là Pending)
app.MapPost("/api/bookings", async (BookingRequest request, AppDbContext db) =>
{
    var ev = await db.Events.FindAsync(request.EventId);
    if (ev is null)
        return Results.NotFound(new { Message = "Không tìm thấy sự kiện." });

    if (request.Quantity <= 0)
        return Results.BadRequest(new { Message = "Số lượng vé phải lớn hơn 0." });

    if (ev.AvailableTickets < request.Quantity)
        return Results.BadRequest(new { Message = "Không đủ số lượng vé còn lại." });

    var booking = new Booking
    {
        EventId = request.EventId,
        CustomerName = request.CustomerName,
        Quantity = request.Quantity,
        Status = "Pending",
        CreatedAt = DateTime.UtcNow
    };

    db.Bookings.Add(booking);
    await db.SaveChangesAsync();

    return Results.Created($"/api/bookings/{booking.Id}", booking);
});

// 3. Theo dõi trạng thái một đơn đặt vé theo Id
app.MapGet("/api/bookings/{id:int}", async (int id, AppDbContext db) =>
{
    var booking = await db.Bookings.FindAsync(id);
    return booking is not null 
        ? Results.Ok(booking) 
        : Results.NotFound(new { Message = "Không tìm thấy đơn đặt vé." });
});

// 4. Xem danh sách tất cả vé đã đặt
app.MapGet("/api/bookings", async (AppDbContext db) =>
{
    var bookings = await db.Bookings.OrderByDescending(b => b.CreatedAt).ToListAsync();
    return Results.Ok(bookings);
});

app.Run();

// Model nhận dữ liệu gửi lên khi đặt vé
record BookingRequest(int EventId, string CustomerName, int Quantity);