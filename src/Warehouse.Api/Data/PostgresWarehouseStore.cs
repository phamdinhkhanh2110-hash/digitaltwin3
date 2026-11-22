namespace Warehouse.Api.Data;

using Npgsql;
using Warehouse.Api.Models;

// Lớp thật đọc dữ liệu từ PostgreSQL, thay cho SampleWarehouseStore (dữ liệu gõ cứng).
// Service và Controller không biết dữ liệu lấy từ đâu, chỉ làm việc với IWarehouseStore.
public class PostgresWarehouseStore(IConfiguration config) : IWarehouseStore
{
    // Chuỗi kết nối lấy từ cấu hình (ConnectionStrings:Warehouse)
    private readonly string _connectionString = config.GetConnectionString("Warehouse")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Warehouse.");

    public Room? GetRoom(int id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // 1. Phòng
        Room? room = null;
        using (var cmd = new NpgsqlCommand(
            @"SELECT ""Id"", ""Name"", ""Width"", ""Depth"", ""GridSize"", ""MinGap"", ""Margin"", ""Version""
              FROM ""Rooms"" WHERE ""Id"" = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                room = new Room
                {
                    Id = reader.GetInt32(0), Name = reader.GetString(1),
                    Width = reader.GetDouble(2), Depth = reader.GetDouble(3),
                    GridSize = reader.GetDouble(4), MinGap = reader.GetDouble(5), Margin = reader.GetDouble(6),
                    Version = reader.GetInt32(7),
                };
            }
        }
        if (room == null) return null;     // không có kho này

        // 2. Kệ của phòng (số tầng, chiều cao tầng, nóc, tải trọng tính từ bảng Levels)
        using (var cmd = new NpgsqlCommand(
            @"SELECT k.""Id"", k.""Code"", k.""X"", k.""Y"", k.""Rotation"",
                     COUNT(l.""Id"") FILTER (WHERE NOT l.""IsTop""),
                     COALESCE(MAX(l.""Height"") FILTER (WHERE NOT l.""IsTop""), 0),
                     k.""Bays"", k.""BayWidth"", k.""Depth"",
                     COALESCE(BOOL_OR(l.""IsTop""), false),
                     COALESCE(MAX(l.""MaxWeight"") FILTER (WHERE NOT l.""IsTop""), 0),
                     k.""ProductTypeId""
              FROM ""Racks"" k
              LEFT JOIN ""Levels"" l ON l.""RackId"" = k.""Id""
              WHERE k.""RoomId"" = @id
              GROUP BY k.""Id""
              ORDER BY k.""Id""", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                room.Racks.Add(new Rack
                {
                    Id = reader.GetInt32(0), Code = reader.GetString(1),
                    X = reader.GetDouble(2), Y = reader.GetDouble(3), Rotation = reader.GetInt32(4),
                    Levels = (int)reader.GetInt64(5),               // COUNT trả bigint
                    LevelHeight = reader.GetDouble(6),
                    Bays = reader.GetInt32(7), BayWidth = reader.GetDouble(8), Depth = reader.GetDouble(9),
                    HasTop = reader.GetBoolean(10), MaxWeight = reader.GetDouble(11),
                    ProductTypeId = reader.IsDBNull(12) ? null : reader.GetInt32(12),   // cột cho phép null
                });
            }
        }

        // 3. Tường/cột của phòng
        using (var cmd = new NpgsqlCommand(
            @"SELECT ""Id"", ""Kind"", ""X"", ""Y"", ""Width"", ""Depth""
              FROM ""Obstacles"" WHERE ""RoomId"" = @id ORDER BY ""Id""", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                room.Obstacles.Add(new Obstacle
                {
                    Id = reader.GetInt32(0), Kind = Enum.Parse<ObstacleKind>(reader.GetString(1)),
                    X = reader.GetDouble(2), Y = reader.GetDouble(3),
                    Width = reader.GetDouble(4), Depth = reader.GetDouble(5),
                });
            }
        }

        return room;
    }
    sdsadsadsadsadsad
    // Interface có hàm này nên lớp phải cài đặt, nếu không sẽ không build được
    public IReadOnlyList<Slot> GetSlotsOfRacks(IReadOnlyCollection<int> rackIds)
    {
        var slots = new List<Slot>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(
            @"SELECT s.""Id"", l.""RackId"", s.""Code"", l.""Number"", s.""Position"", l.""IsTop""
              FROM ""Slots"" s
              JOIN ""Levels"" l ON l.""Id"" = s.""LevelId""
              WHERE l.""RackId"" = ANY(@ids)
              ORDER BY l.""RackId"", l.""Number"", s.""Position""", conn);
        cmd.Parameters.AddWithValue("ids", rackIds.ToArray());

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            slots.Add(new Slot
            {
                Id = reader.GetInt32(0), RackId = reader.GetInt32(1), Code = reader.GetString(2),
                Level = reader.GetInt32(3), Bay = reader.GetInt32(4), IsTop = reader.GetBoolean(5),
            });
        }
        return slots;
    }
}
