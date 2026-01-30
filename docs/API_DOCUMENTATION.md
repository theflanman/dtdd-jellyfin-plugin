# DoesTheDogDie API Documentation

Reverse-engineered API documentation for DoesTheDogDie.com integration.

**Last Updated:** 2026-01-30
**API Version:** Unofficial (reverse-engineered)

---

## Authentication

### Headers Required

| Header | Value | Required |
|--------|-------|----------|
| `Accept` | `application/json` | Yes |
| `X-API-KEY` | Your API key | Recommended |

**Note:** Testing revealed the API may return results even without a valid API key. However, using a valid key is recommended for reliability and to respect rate limits.

**Obtaining an API Key:**
- Create account at doesthedogdie.com
- Navigate to profile page
- API key is displayed there

---

## Endpoints

### 1. Search Media

**URL:** `GET https://www.doesthedogdie.com/dddsearch`

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `q` | string | Search query (URL encoded) |
| `imdb` | string | IMDB ID (e.g., `tt2911666`) |

**Note:** Use `q` OR `imdb`, not both.

#### Example Requests

```bash
# Search by title
curl -H "Accept: application/json" -H "X-API-KEY: YOUR_KEY" \
  "https://www.doesthedogdie.com/dddsearch?q=John+Wick"

# Search by IMDB ID
curl -H "Accept: application/json" -H "X-API-KEY: YOUR_KEY" \
  "https://www.doesthedogdie.com/dddsearch?imdb=tt2911666"
```

#### Response Schema

```json
{
  "items": [
    {
      "id": 15713,
      "name": "John Wick",
      "cleanName": "john wick",
      "altName": "",
      "genre": "action",
      "releaseYear": "2014",
      "tmdbId": 245891,
      "imdbId": "tt2911666",
      "posterImage": "wXqWR7dHncNRbxoEGybEy7QTe9h.jpg",
      "backgroundImage": "ff2ti5DkA9UYLzyqhQfI2kZqEuh.jpg",
      "overview": "Ex-hitman John Wick comes out of retirement...",
      "numRatings": 11399,
      "verified": true,
      "staffVerified": true,
      "ItemTypeId": 15,
      "itemType": {
        "id": 15,
        "name": "Movie",
        "index1": null,
        "index2": null
      },
      "createdAt": "2018-08-02T01:33:07.000Z",
      "updatedAt": "2026-01-27T12:05:21.000Z"
    }
  ],
  "topics": []
}
```

#### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | DTDD internal ID (use for `/media/{id}`) |
| `name` | string | Display title |
| `cleanName` | string | Normalized title (lowercase, no articles) |
| `releaseYear` | string | Year of release |
| `genre` | string | Primary genre |
| `tmdbId` | number | TMDB ID (nullable) |
| `imdbId` | string | IMDB ID (nullable) |
| `posterImage` | string | TMDB poster path (prepend TMDB base URL) |
| `backgroundImage` | string | TMDB backdrop path |
| `overview` | string | Synopsis |
| `numRatings` | number | Total trigger votes |
| `verified` | boolean | Content verified |
| `staffVerified` | boolean | Staff verified |
| `ItemTypeId` | number | Media type ID |
| `itemType` | object | Media type details |

#### Item Type IDs

| ID | Name |
|----|------|
| 14 | Book |
| 15 | Movie |
| 16 | TV Show |
| 17 | Video Game |
| 18 | Short Story |
| 19 | Blog |
| 20 | Magazine |
| 21 | Podcast |
| 22 | Comic Book |
| 23 | Anime |
| 24 | Manga |
| 26 | YouTube |
| 27 | Stage Play |
| 28 | Broadway Musical |

---

### 2. Get Media Details

**URL:** `GET https://www.doesthedogdie.com/media/{id}`

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | number | DTDD media ID from search results |

#### Example Request

```bash
curl -H "Accept: application/json" -H "X-API-KEY: YOUR_KEY" \
  "https://www.doesthedogdie.com/media/15713"
```

#### Response Schema

```json
{
  "item": {
    "id": 15713,
    "name": "John Wick",
    "imdbId": "tt2911666",
    "tmdbId": 245891,
    "itemType": {
      "id": 15,
      "name": "Movie"
    }
    // ... same fields as search result
  },
  "topicItemStats": [
    {
      "topicItemId": 504377,
      "yesSum": 1336,
      "noSum": 118,
      "numComments": 59,
      "TopicId": 153,
      "ItemId": 15713,
      "comment": "Yes, and it's terrible, BUT John Wick spends the rest of the movie deliberately, gloriously, and violently avenging the dog...",
      "username": "egreen",
      "comments": [
        {
          "id": 785021,
          "voteSum": 304,
          "comment": "Yes, and it's terrible...",
          "index1": -1,
          "index2": -1,
          "User": {
            "id": 31856,
            "displayName": "egreen"
          }
        }
      ],
      "topic": {
        "id": 153,
        "name": "a dog dies",
        "notName": "no dogs die",
        "survivesName": "the dog survives",
        "doesName": "Does the dog die",
        "description": "This trigger is for people who are upset by the death of a canine companion...",
        "isSpoiler": false,
        "isSensitive": false,
        "isVisible": true,
        "smmwDescription": "dogs dying",
        "TopicCategoryId": 2,
        "TopicCategory": {
          "id": 2,
          "name": "Animal"
        }
      },
      "TopicCategory": {
        "id": 2,
        "name": "Animal"
      },
      "slug": "does-the-dog-die"
    }
  ]
}
```

#### TopicItemStats Fields

| Field | Type | Description |
|-------|------|-------------|
| `topicItemId` | number | Unique ID for this topic-item pair |
| `yesSum` | number | "Yes" votes for this trigger |
| `noSum` | number | "No" votes for this trigger |
| `numComments` | number | Number of user comments |
| `TopicId` | number | Topic/trigger ID |
| `ItemId` | number | Media item ID |
| `comment` | string | Top-voted comment |
| `username` | string | Comment author |
| `comments` | array | Array of comment objects |
| `topic` | object | Full topic details |
| `TopicCategory` | object | Category of the topic |
| `slug` | string | URL-friendly topic name |

#### Topic Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | Topic ID |
| `name` | string | Trigger name (e.g., "a dog dies") |
| `notName` | string | Negative form (e.g., "no dogs die") |
| `survivesName` | string | Survival form (e.g., "the dog survives") |
| `doesName` | string | Question form (e.g., "Does the dog die") |
| `description` | string | Detailed description |
| `isSpoiler` | boolean | Contains spoilers |
| `isSensitive` | boolean | Sensitive content |
| `isVisible` | boolean | Publicly visible |
| `smmwDescription` | string | Short description |
| `TopicCategoryId` | number | Category ID |
| `TopicCategory` | object | Category details |

#### Topic Categories

| ID | Name |
|----|------|
| 2 | Animal |
| 3 | Violence |
| (varies) | Mental Health, Sexual, Phobias, etc. |

---

## Error Handling

### Invalid IMDB ID

**Response:** HTTP 200 with empty results
```json
{
  "items": [],
  "topics": []
}
```

### Invalid Media ID

**Response:** HTTP 404 with HTML error page (not JSON!)

**Handling:** Check Content-Type header or try parsing JSON; if it fails, treat as not found.

### Authentication Errors

Testing revealed the API may not strictly enforce API keys. However, implement proper error handling:

| HTTP Code | Meaning | Action |
|-----------|---------|--------|
| 200 | Success | Process response |
| 401 | Unauthorized | Check API key |
| 404 | Not found | Item doesn't exist |
| 429 | Rate limited | Back off and retry |
| 500 | Server error | Retry with backoff |

---

## Rate Limits

**Status:** No official documentation found.

**Recommendations:**
- Implement caching (1-7 days for trigger data)
- Rate limit requests to 1/second
- Use exponential backoff on errors
- Batch requests where possible

---

## Image URLs

Poster and background images are TMDB paths. Construct full URLs:

```
https://image.tmdb.org/t/p/w500/{posterImage}
https://image.tmdb.org/t/p/original/{backgroundImage}
```

---

## Interpreting Trigger Results

```csharp
// Determine if trigger applies
if (topicItemStat.YesSum > topicItemStat.NoSum)
{
    // Trigger applies (e.g., "a dog dies" = YES)
    confidence = (double)topicItemStat.YesSum / (topicItemStat.YesSum + topicItemStat.NoSum);
}
else if (topicItemStat.NoSum > topicItemStat.YesSum)
{
    // Trigger does NOT apply (e.g., "no dogs die")
    confidence = (double)topicItemStat.NoSum / (topicItemStat.YesSum + topicItemStat.NoSum);
}
else
{
    // Inconclusive (equal votes)
}
```

---

## C# Model Classes

```csharp
public class DtddSearchResponse
{
    public List<DtddMediaItem> Items { get; set; } = new();
    public List<DtddTopic> Topics { get; set; } = new();
}

public class DtddMediaItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CleanName { get; set; }
    public string? Genre { get; set; }
    public string? ReleaseYear { get; set; }
    public int? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? PosterImage { get; set; }
    public string? BackgroundImage { get; set; }
    public string? Overview { get; set; }
    public int NumRatings { get; set; }
    public bool Verified { get; set; }
    public bool StaffVerified { get; set; }
    public int ItemTypeId { get; set; }
    public DtddItemType? ItemType { get; set; }
}

public class DtddItemType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DtddMediaDetails
{
    public DtddMediaItem Item { get; set; } = new();
    public List<DtddTopicItemStat> TopicItemStats { get; set; } = new();
}

public class DtddTopicItemStat
{
    public int TopicItemId { get; set; }
    public int YesSum { get; set; }
    public int NoSum { get; set; }
    public int NumComments { get; set; }
    public int TopicId { get; set; }
    public int ItemId { get; set; }
    public string? Comment { get; set; }
    public string? Username { get; set; }
    public DtddTopic? Topic { get; set; }
    public DtddTopicCategory? TopicCategory { get; set; }
    public string? Slug { get; set; }
}

public class DtddTopic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NotName { get; set; }
    public string? SurvivesName { get; set; }
    public string? DoesName { get; set; }
    public string? Description { get; set; }
    public bool IsSpoiler { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsVisible { get; set; }
    public string? SmmwDescription { get; set; }
    public int? TopicCategoryId { get; set; }
    public DtddTopicCategory? TopicCategory { get; set; }
}

public class DtddTopicCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

---

## Test Data

| Title | IMDB ID | DTDD ID | Notable Triggers |
|-------|---------|---------|------------------|
| John Wick | tt2911666 | 15713 | Dog death, violence, animal abuse |
| Marley & Me | tt0822832 | ? | Dog death |
| Game of Thrones | tt0944947 | ? | Violence, nudity, animal death |
| A Quiet Place | tt6644200 | ? | Jump scares, child peril |

---

## References

- Official (limited): https://www.doesthedogdie.com/api
- TypeScript wrapper: https://github.com/jayshoo/doesthedogdie-api
