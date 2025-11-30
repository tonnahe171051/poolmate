# Player List API Documentation

## Endpoint: GET /api/players

Get a paginated list of all players with optional filtering.

### Authorization
- **Public endpoint** - No authentication required (AllowAnonymous)

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `pageIndex` | int | No | 1 | Page number (1-based) |
| `pageSize` | int | No | 20 | Number of items per page |
| `hasSkillLevel` | bool? | No | null | Filter players by skill level status:<br/>- `null`: All players<br/>- `true`: Only players WITH skill level<br/>- `false`: Only players WITHOUT skill level |
| `searchTerm` | string | No | null | Search by player name or nickname (case-insensitive) |
| `country` | string | No | null | Filter by country code (2-letter ISO code, e.g., "US", "VN") |

### Response Format

```json
{
  "success": true,
  "message": "Success",
  "statusCode": 200,
  "data": {
    "pageIndex": 1,
    "totalPages": 5,
    "totalRecords": 100,
    "pageSize": 20,
    "hasPreviousPage": false,
    "hasNextPage": true,
    "items": [
      {
        "id": 1,
        "fullName": "Nguyen Van A",
        "slug": "nguyen-van-a",
        "nickname": "Pro Player",
        "country": "VN",
        "city": "Ho Chi Minh",
        "skillLevel": 7,
        "createdAt": "2025-01-15T10:30:00Z"
      },
      {
        "id": 2,
        "fullName": "Tran Thi B",
        "slug": "tran-thi-b",
        "nickname": null,
        "country": "VN",
        "city": "Ha Noi",
        "skillLevel": null,
        "createdAt": "2025-01-16T14:20:00Z"
      }
    ]
  }
}
```

### Example Usage

#### 1. Get all players (first page)
```
GET /api/players
```

#### 2. Get players with skill level only
```
GET /api/players?hasSkillLevel=true&pageIndex=1&pageSize=20
```

#### 3. Get players without skill level
```
GET /api/players?hasSkillLevel=false&pageIndex=1&pageSize=10
```

#### 4. Search by name
```
GET /api/players?searchTerm=nguyen&pageIndex=1&pageSize=20
```

#### 5. Filter by country
```
GET /api/players?country=VN&pageIndex=1&pageSize=20
```

#### 6. Combined filters
```
GET /api/players?hasSkillLevel=true&country=VN&searchTerm=nguyen&pageIndex=1&pageSize=20
```

### Frontend Routing
Each player item includes a `slug` field for SEO-friendly URLs. Use it to navigate to player detail pages:

```javascript
// Example: Navigate to player detail page
const playerDetailUrl = `/players/${player.slug}`;
// Result: /players/nguyen-van-a
```

### Related Endpoints

- **Get Player Detail**: `GET /api/players/{slug}` - Get detailed information about a specific player by slug
- **Get Player Stats**: `GET /api/players/{playerId}/stats` - Get statistics for a specific player
- **Get Player Matches**: `GET /api/players/{playerId}/matches` - Get match history for a specific player
- **Get Player Tournaments**: `GET /api/players/{playerId}/tournaments` - Get tournament history for a specific player

### Notes

1. **Slug Field**: Always included in the response for frontend routing
2. **Case-Insensitive Search**: The `searchTerm` parameter performs case-insensitive matching
3. **Skill Level**: Can be `null` if the player hasn't set their skill level yet
4. **Pagination**: Standard pagination with page index starting from 1
5. **Default Sorting**: Players are sorted alphabetically by full name

