using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

const string dbPath = "conway.sqlite";
var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddDbContext<GameDb>(opt => opt.UseInMemoryDatabase("int"));
builder.Services.AddDbContext<GameDb>(opt => opt.UseSqlite($"Data Source={dbPath}"));
var app = builder.Build();

app.MapGet("/", () => "netdocuments exercise");

app.MapGet("/games/", (GameDb db) =>
{
    db.Database.EnsureCreated();
    return db.Games switch
    {
        { } internalDbSet => Results.Ok(internalDbSet),
        _ => Results.NotFound(),
    };
});

app.MapPost("/games/", async (GameDb db, [FromBody] int[]? boardState, [FromQuery] int? columns, [FromQuery] int? rows) =>
{
    var numColumns = columns ?? 10;
    var numRows = rows ?? 10;
    var game = new Game
    {
        Columns = numColumns,
        Rows = numRows,
        BoardState = boardState ?? Enumerable.Range(0, numColumns * numRows).Select(i => Random.Shared.Next(2)).ToArray(),
    };
    db.Add(game);
    await db.SaveChangesAsync();
    return Results.Ok(game.Id);
});

app.MapGet("/games/{id}", async (string id, GameDb db) =>
    await db.FindAsync<Game>(int.Parse(id)) switch
        {
            {} game => Results.Ok(game),
            _ => Results.NotFound(),
        }
);

app.MapPut("/games/{id}/step", async (string id, GameDb db) =>
    {
        var game = await db.FindAsync<Game>(int.Parse(id));
        if (game == null)
            return Results.NotFound();
        game.Advance();
        db.Update(game);
        await db.SaveChangesAsync();
        return Results.Ok(game);
    }
);

app.MapPut("/games/{id}/step/{steps}", async (string id, int steps, GameDb db) =>
    {
        var game = await db.FindAsync<Game>(int.Parse(id));
        if (game == null)
            return Results.NotFound();
        do
        {
            game.Advance();
        } while (game.BoardState.Sum() != 0 && steps-- > 1);
        db.Update(game);
        await db.SaveChangesAsync();
        return Results.Ok(game);
    }
);

app.MapPut("/games/{id}/step/complete", async (string id, [FromQuery] int? maxGenerations, GameDb db) =>
    {
        var game = await db.FindAsync<Game>(int.Parse(id));
        if (game == null)
            return Results.NotFound();
        var complete = CompleteGame(game, maxGenerations ?? 10);
        db.Update(game);
        await db.SaveChangesAsync();
        if (!complete) return Results.NotFound();
        return Results.Ok(game);
    }
);

app.Run();



bool CompleteGame(Game game, int generations)
{
    HashSet<int[]> boardStates = new HashSet<int[]>();
    for (var i = 0; i < generations; i++)
    {
        var newState = game.Advance();
        // if everybody is dead or we're repeating
        if (newState.Sum() == 0 || !boardStates.Add(newState))
            return true;
    }
    return false;
}

internal class Game
{
    public int Id { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; } = 10;
    public int Generation { get; set; } = 1;
    public int[] BoardState { get; set; } = [];
    private int Idx(int row, int col) => ((row + Rows) % Rows) * Columns + ((col + Columns) % Columns);
    public int[] Advance()
    {
        var newState = (int[])BoardState.Clone();
        for (var col = 0; col < Columns; col++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var idx = Idx(row, col);
                newState[idx] = (BoardState[idx], CountNeighbors(col, row)) switch
                {
                    (0, 3) => 1,
                    (1, < 2) => 0,
                    (1, > 3) => 0,
                    var (a, _) => a,
                };
            }
        }
        Generation++;
        BoardState = newState;
        return BoardState;
    }

    private int CountNeighbors(int col, int row)
    {
        var sum = 0;
        for (var i = -1; i < 2; i++)
        {
            for (var j = -1; j < 2; j++)
            {
                if (i != 0 || j != 0) // we're not our own neighbor
                    sum += BoardState[Idx(row+i, col+i)];
            }
        }
        return sum;
    }
}

internal class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
}
