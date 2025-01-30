```markdown
# Rock-Paper-Scissors Multiplayer Game

![Architecture Diagram](https://via.placeholder.com/800x400?text=Solution+Architecture+Diagram)

A hybrid REST/gRPC solution for multiplayer Rock-Paper-Scissors games with real-time gameplay and currency transactions.

## Table of Contents
- [Solution Structure](#solution-structure)
- [Technical Stack](#technical-stack)
- [Getting Started](#getting-started)
- [API Endpoints](#api-endpoints)
- [Client Commands](#client-commands)
- [Database Setup](#database-setup)
- [Key Implementation Details](#key-implementation-details)
- [Troubleshooting](#troubleshooting)

---

## Solution Structure

```
RockPaperScissors/
├── RockPaperScissorsAPI/          # Main backend service (ASP.NET Core 9.0)
├── RockPaperScissorsClient/       # Console client application
└── RockPaperScissors.gRPC/         # c# class library

```

---

## Technical Stack

- **Backend**:
  - ASP.NET Core 9.0
  - PostgreSQL with Entity Framework Core
  - gRPC for real-time communication
  - REST API for transactional operations
  - ASP.NET Core Identity for user management

- **Client**:
  - .NET 9.0 Console Application
  - gRPC client for game operations
  - REST client for transactions

---

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL 16.6
- IDE (VSCode)

### Installation
```bash
# Clone repository
git clone https://github.com/nouarismail/rock-paper-scissors.git
cd rock-paper-scissors


# Apply database migrations
dotnet ef database update --project RockPaperScissors.API
```

---

## API Endpoints

### REST API (Port 5000)
| Endpoint               | Method | Description                     |
|------------------------|--------|---------------------------------|
| `/api/matches/create`         | POST   | Create new match with bet       |
| `/api/transactions/create`    | POST   | Transfer credits between players

### gRPC Services (Port 5001)


---

## Client Commands

```bash
1. Create Game        # Start new match (REST)
2. View Balance       # Check credits (REST)
3. List Games         # Show available matches (gRPC)
4. Join Game          # Enter existing match (gRPC)
5. Transfer Credits   # Send credits to player (REST)
6. Exit               # Quit application
```

---



### Seeded Users
```csharp
public static async Task SeedData(AppDbContext dataContext,UserManager<ApplicationUser> userManager)
        {
            if (!userManager.Users.Any())
            {
                var users = new List<ApplicationUser>
                {
                    new ApplicationUser
                    {
                        Id = "a",
                        DisplayName = "Bob",
                        UserName = "bob",
                        Email = "bob@test.com"
                    },
                    new ApplicationUser
                    {
                        Id = "b",
                        DisplayName = "Jane",
                        UserName = "jane",
                        Email = "jane@test.com"
                    },
                    new ApplicationUser
                    {
                        Id = "c",
                        DisplayName = "Tom",
                        UserName = "tom",
                        Email = "tom@test.com"
                    },
                };

                foreach (var user in users)
                {
                    await userManager.CreateAsync(user, "Pa$$w0rd");
                }
            }

            await dataContext.SaveChangesAsync();
        }
```

---

## Key Implementation Details

### Match State Management
```csharp
public class MatchState
{
    
    public string MatchId { get; } = Guid.NewGuid().ToString();
    public string Player1Id { get; set; }
    public string Player2Id { get; set; }
    public List<IServerStreamWriter<JoinGameResponse>> JoinStreams { get; } = new();

    
    public string Player1Move { get; set; }
    public string Player2Move { get; set; }
    public List<IServerStreamWriter<MoveResponse>> MoveStreams { get; } = new();
    
    
    private readonly object _lock = new();
    
    public void AddPlayer(string userId, IServerStreamWriter<JoinGameResponse> stream)
    {
        lock (_lock)
        {
            if (Player1Id == null) Player1Id = userId;
            else if (Player2Id == null) Player2Id = userId;
            JoinStreams.Add(stream);
        }
    }
}
```

### Hybrid Communication Flow
1. **Game Creation** (REST)
   ```mermaid
   sequenceDiagram
       Client->>API: POST /api/matches/create
       API->>Database: Create match record
       API-->>Client: Return match ID
   ```

2. **Real-Time Gameplay** (gRPC)
   ```mermaid
   sequenceDiagram
       Client->>gRPC: JoinGame(matchId)
       gRPC->>MatchManager: Add to JoinStreams
       MatchManager->>Clients: Broadcast READY status
       Clients->>gRPC: SubmitMove()
       gRPC->>MatchManager: Track moves
       MatchManager->>Clients: Broadcast results
   ```

---

