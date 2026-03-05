# DoesTheDogDie API Documentation

Reverse-engineered API documentation for DoesTheDogDie.com integration.

**Last Updated:** 2026-03-05
**API Version:** Unofficial (reverse-engineered, based on site's "v1.1" label)

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

**Alias:** `GET https://www.doesthedogdie.com/search` (identical behavior)

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `q` | string | Search query (URL encoded) |
| `imdb` | string | IMDB ID (e.g., `tt2911666`) |

**Note:** Use `q` OR `imdb`, not both. The `tmdb` parameter is NOT supported (returns 500 error). Parameters `page`, `limit`, `offset`, and `itemType` are accepted but ignored — the API returns all matching results in a single response.

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
      "cleanNameArticles": "john wick",
      "altName": "john wick i",
      "genre": "action",
      "releaseYear": "2014",
      "tmdbId": 245891,
      "imdbId": "tt2911666",
      "posterImage": "wXqWR7dHncNRbxoEGybEy7QTe9h.jpg",
      "backgroundImage": "ff2ti5DkA9UYLzyqhQfI2kZqEuh.jpg",
      "overview": "Ex-hitman John Wick comes out of retirement...",
      "review": "",
      "numRatings": 11499,
      "verified": 0,
      "posterVerified": 1,
      "backgroundVerified": 1,
      "staffVerified": true,
      "adult": 0,
      "verifyAttempts": 100,
      "stats": "{\"topics\":{\"153\":{\"definitelyYes\":1,\"definitelyNo\":0},...}}",
      "ItemTypeId": 15,
      "itemType": {
        "id": 15,
        "name": "Movie"
      },
      "minStaffIndex1": -1,
      "minStaffIndex2": -1,
      "maxStaffIndex1": -1,
      "maxStaffIndex2": -1,
      "createdAt": "2018-08-02T01:33:07.000Z",
      "updatedAt": "2026-03-03T11:00:02.000Z"
    }
  ],
  "topics": [
    {
      "id": 153,
      "name": "a dog dies",
      "notName": "no dogs die",
      "survivesName": "the dog survives",
      "keywords": "Dog death, canine death, pet dog death, puppy dies",
      "doesName": "Does the dog die",
      "TopicCategoryId": 2,
      "TopicSubCategoryId": 22,
      "supporters": 720,
      "isSpoiler": 0,
      "isVisible": 1,
      "isSensitive": 0,
      "smmwDescription": "dogs dying"
    }
  ]
}
```

#### Response Fields — Items

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | DTDD internal ID (use for `/media/{id}`) |
| `name` | string | Display title |
| `cleanName` | string | Normalized title (lowercase, no articles) |
| `cleanNameArticles` | string | Normalized title preserving articles |
| `altName` | string? | Alternate title |
| `releaseYear` | string | Year of release |
| `genre` | string | Primary genre |
| `tmdbId` | number? | TMDB ID |
| `imdbId` | string? | IMDB ID |
| `posterImage` | string? | TMDB poster path (prepend TMDB base URL) |
| `backgroundImage` | string? | TMDB backdrop path |
| `overview` | string? | Synopsis |
| `review` | string? | Editorial review text |
| `numRatings` | number | Total trigger votes |
| `verified` | number | Content verified (0/1) |
| `posterVerified` | number | Poster image verified (0/1) |
| `backgroundVerified` | number | Background image verified (0/1) |
| `staffVerified` | boolean | Staff verified |
| `adult` | number | Adult content flag (0/1) |
| `verifyAttempts` | number | Number of verification attempts |
| `stats` | string | **JSON-encoded string** with quick-look trigger results per topic ID |
| `ItemTypeId` | number | Media type ID |
| `itemType` | object | Media type details |
| `minStaffIndex1` | number | Min staff-verified season (-1 = none) |
| `minStaffIndex2` | number | Min staff-verified episode (-1 = none) |
| `maxStaffIndex1` | number | Max staff-verified season (-1 = none) |
| `maxStaffIndex2` | number | Max staff-verified episode (-1 = none) |

#### The `stats` Field (Embedded JSON)

The `stats` field is a **JSON string** (not an object) that provides a quick summary of trigger results without needing to call `/media/{id}`. Parse it separately:

```json
{
  "topics": {
    "153": { "definitelyYes": 1, "definitelyNo": 0 },
    "158": { "definitelyYes": 0, "definitelyNo": 1 }
  }
}
```

Each key is a topic ID. `definitelyYes`/`definitelyNo` are 0 or 1 indicating the consensus. This allows bulk-checking triggers from search results without individual `/media/` calls.

#### Response Fields — Topics

When searching with `q`, matching trigger topics are also returned:

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | Topic ID |
| `name` | string | Trigger name |
| `keywords` | string | Comma-separated search keywords |
| `doesName` | string | Question form |
| `TopicCategoryId` | number | Category ID |
| `TopicSubCategoryId` | number? | Subcategory ID |
| `supporters` | number | Number of paid supporters for this topic |

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
  "item": { ... },
  "topicItemStats": [ ... ],
  "allGroups": [
    {
      "name": "Your Triggers",
      "topics": [ ... ]
    },
    {
      "name": "Needs More Answers",
      "topics": []
    }
  ],
  "smartPageTitle": null,
  "smartPageDescription": null,
  "index1": null,
  "index2": null,
  "numYes": 65,
  "numNo": 132
}
```

#### Top-Level Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `item` | object | Full media item details |
| `topicItemStats` | array | All trigger results for this media |
| `allGroups` | array | Triggers grouped by category (e.g., "Your Triggers", "Needs More Answers") |
| `smartPageTitle` | string? | SEO page title |
| `smartPageDescription` | string? | SEO page description |
| `index1` | number? | For TV shows: current season filter |
| `index2` | number? | For TV shows: current episode filter |
| `numYes` | number | Count of triggers with "yes" consensus |
| `numNo` | number | Count of triggers with "no" consensus |

#### Item Fields (additional to search)

| Field | Type | Description |
|-------|------|-------------|
| `isPurchased` | boolean | Whether current user has purchased supporter access |
| `art` | string? | Legacy art URL |

#### TopicItemStats Fields

| Field | Type | Description |
|-------|------|-------------|
| `topicItemId` | number | Unique ID for this topic-item pair |
| `yesSum` | number | "Yes" votes for this trigger |
| `noSum` | number | "No" votes for this trigger |
| `numComments` | number | Number of user comments |
| `TopicId` | number | Topic/trigger ID |
| `ItemId` | number | Media item ID |
| `RatingId` | number? | ID of the top-rated comment |
| `commentUserIds` | string | Comma-separated user IDs who commented |
| `voteSum` | number | Vote sum of top comment |
| `comment` | string? | Top-voted comment text |
| `isAnonymous` | number | Whether top comment is anonymous (0/1) |
| `username` | string? | Comment author display name |
| `UserId` | number? | Comment author user ID |
| `isYes` | number | Whether consensus is yes (1) or no (0) |
| `index1` | number | Season number (-1 = all/none) |
| `index2` | number | Episode number (-1 = all/none) |
| `ratingIndex1` | number | Rating season (-1 = all/none) |
| `ratingIndex2` | number | Rating episode (-1 = all/none) |
| `itemTypeIndex1` | any | Item type season label |
| `itemTypeIndex2` | any | Item type episode label |
| `hasUserComment` | boolean | Whether current user has commented |
| `isFavorite` | boolean | Whether current user favorited this topic |
| `comments` | array | Array of comment objects |
| `topic` | object | Full topic details |
| `doesName` | string | Question form of topic |
| `TopicCategory` | object | Category details |
| `slug` | string | URL-friendly topic name |

#### Topic Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | Topic ID |
| `name` | string | Trigger name (e.g., "a dog dies") |
| `notName` | string | Negative form (e.g., "no dogs die") |
| `survivesName` | string? | Survival form (e.g., "the dog survives") |
| `doesName` | string | Question form (e.g., "Does the dog die") |
| `keywords` | string | Search keywords |
| `description` | string | Detailed description |
| `listName` | string | List label (e.g., "where the dog dies") |
| `image` | string | Topic icon name or path |
| `ordering` | number | Display order |
| `demandOrder` | number | Demand-based order |
| `isSpoiler` | boolean | Contains spoilers |
| `isSensitive` | boolean | Sensitive content |
| `isVisible` | boolean | Publicly visible |
| `smmwDescription` | string | Short description |
| `legacyId` | number? | Legacy system ID |
| `supporters` | number | Number of paid supporters |
| `TopicCategoryId` | number | Category ID |
| `TopicCategory` | object | Category details |

---

### 3. List All Topics/Triggers (UNDOCUMENTED)

**URL:** `GET https://www.doesthedogdie.com/categories`

Returns the complete list of all 204 trigger topics with their categories. No parameters needed.

#### Example Request

```bash
curl -H "Accept: application/json" -H "X-API-KEY: YOUR_KEY" \
  "https://www.doesthedogdie.com/categories"
```

#### Response Schema

Returns a JSON array of topic objects:

```json
[
  {
    "id": 153,
    "name": "a dog dies",
    "notName": "no dogs die",
    "survivesName": "the dog survives",
    "keywords": "Dog death, canine death, pet dog death, puppy dies",
    "description": "...",
    "subtitle": "",
    "subtitleText": null,
    "subtitleUrl": null,
    "doesName": "Does the dog die",
    "listName": "where the dog dies",
    "image": "dog",
    "ordering": 100,
    "demandOrder": 1,
    "isSpoiler": false,
    "isVisible": true,
    "isSensitive": false,
    "smmwDescription": "dogs dying",
    "legacyId": 25,
    "supporters": 720,
    "TopicCategoryId": 2,
    "TopicCategory": {
      "id": 2,
      "name": "Animal"
    },
    "slug": "does-the-dog-die",
    "isFavorite": true
  }
]
```

#### Topic Categories (Complete List)

| Category | Topic Count |
|----------|-------------|
| Abandonment | 3 |
| Abuse | 9 |
| Addiction | 3 |
| Animal | 17 |
| Assault | 11 |
| Bodily Harm | 29 |
| Children | 3 |
| Creepy Crawly | 1 |
| Death | 4 |
| Disability | 2 |
| Drugs/Alcohol | 1 |
| Family | 5 |
| Fear | 9 |
| Gross | 7 |
| LGBTQ+ | 5 |
| Large-scale Violence | 1 |
| Law Enforcement | 2 |
| Live Theatre | 5 |
| Loss | 1 |
| Medical | 7 |
| Mental Health | 20 |
| Natural Disasters | 1 |
| Noxious | 7 |
| Paranoia | 2 |
| Pregnancy | 6 |
| Prejudice | 12 |
| Race | 1 |
| Relationships | 1 |
| Religious | 2 |
| Sex | 7 |
| Sexism | 1 |
| Sickness | 4 |
| Social | 4 |
| Spoiler | 3 |
| Vehicular | 4 |
| Violence | 4 |

**Total: 204 topics across 36 categories**

---

### 4. Browse Topic by Slug (UNDOCUMENTED)

**URL:** `GET https://www.doesthedogdie.com/{topic-slug}`

Returns paginated list of media items with their trigger results for a specific topic. The slug comes from the topic's `slug` field (e.g., `does-the-dog-die`, `are-there-spiders`).

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | number | 0 | Page number (0-indexed) |
| `yesNo` | string | `"yes"` | Filter: `"yes"` for confirmed triggers, `"no"` for denied |
| `itemType` | number | all | Item type ID filter (accepted but may not filter correctly) |

#### Example Request

```bash
# Get page 2 of movies where the dog dies
curl -H "Accept: application/json" -H "X-API-KEY: YOUR_KEY" \
  "https://www.doesthedogdie.com/does-the-dog-die?page=2&yesNo=yes"
```

#### Response Schema

```json
{
  "topic": {
    "id": 153,
    "name": "a dog dies",
    "slug": "does-the-dog-die",
    ...
  },
  "topicItemStats": [ ... ],
  "topicItemStatsCount": [{ "count": 8966 }],
  "itemTypeId": "all",
  "itemTypeName": "Media",
  "page": 2,
  "totalPages": 299,
  "itemTypes": [
    {
      "id": 15,
      "name": "Movie",
      "slug": "movies",
      "verb": "watch",
      "pastTenseVerb": "watched",
      "index1": null,
      "index2": null,
      "position1": "hour",
      "position2": "minute",
      "position3": "second",
      "sectionName": null,
      "overviewName": null
    }
  ],
  "yesNo": "yes"
}
```

#### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `topic` | object | Full topic details |
| `topicItemStats` | array | 30 items per page with trigger data |
| `topicItemStatsCount` | array | `[{ "count": N }]` total items |
| `page` | number | Current page (0-indexed) |
| `totalPages` | number | Total pages available |
| `itemTypeId` | string | Current filter (`"all"` or type ID) |
| `itemTypeName` | string | Current filter name (`"Media"` or type name) |
| `itemTypes` | array | Complete list of all item types with metadata |
| `yesNo` | string | Current yes/no filter value |

#### Item Types Metadata (from `itemTypes` array)

| Field | Type | Description |
|-------|------|-------------|
| `id` | number | Item type ID |
| `name` | string | Display name |
| `slug` | string | URL slug (e.g., `"movies"`, `"tv-shows"`) |
| `verb` | string | Action verb (`"watch"`, `"read"`, `"play"`, `"listen"`) |
| `pastTenseVerb` | string | Past tense (`"watched"`, `"read"`, `"played"`) |
| `index1` | string? | Primary index label (`"season"` for TV, `"chapter"` for books) |
| `index2` | string? | Secondary index label (`"episode"` for TV) |
| `position1` | string? | Timestamp unit 1 (`"hour"` for movies/TV) |
| `position2` | string? | Timestamp unit 2 (`"minute"`) |
| `position3` | string? | Timestamp unit 3 (`"second"`) |
| `sectionName` | string? | Section label (`"episode"`, `"chapter"`) |
| `overviewName` | string? | Overview label (`"series"`, `"book"`) |

---

## Endpoints Not Found (404)

The following paths were tested and return 404 HTML pages:

- `/topics` — Use `/categories` instead
- `/topic/{id}` — Use `/{topic-slug}` instead
- `/comments/{topicItemId}` — Comments are embedded in `/media/{id}` response
- `/topicitem/{id}` — Not a standalone endpoint
- `/vote` — Voting requires authentication (POST only)

## Endpoints Requiring Authentication

These endpoints exist but require an authenticated session (not just API key):

- `POST /report` — Report content (requires `itemId` param)
- `POST /logincheck` — Validate login credentials
- `POST /additem` — Add new media to the database
- `/profile` — User profile (contains API key)
- `/api/v2/mobile/login` — Mobile app login endpoint
- `/purchase/create` — Stripe payment for supporter status
- `/beta?dontAskAgain=` — Beta feature opt-in
- `/usernameCheck` — Check username availability

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

### Invalid Search Parameter

**Response:** HTTP 500 with HTML error page (e.g., `?tmdb=` param)

### Authentication Errors

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
- Use the `stats` field from search results for quick-look data instead of calling `/media/{id}` for every item

---

## Image URLs

Poster and background images are TMDB paths. Construct full URLs:

```
https://image.tmdb.org/t/p/w500/{posterImage}
https://image.tmdb.org/t/p/original/{backgroundImage}
```

---

## Interpreting Trigger Results

### From `/media/{id}` Response

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

// Also available: topicItemStat.IsYes (1 or 0) for pre-computed consensus
```

### From Search `stats` Field (Quick-Look)

```csharp
// Parse the embedded JSON string
var stats = JsonSerializer.Deserialize<StatsWrapper>(item.Stats);
foreach (var (topicId, result) in stats.Topics)
{
    if (result.DefinitelyYes == 1)
    {
        // Trigger confirmed for this topic
    }
    else if (result.DefinitelyNo == 1)
    {
        // Trigger denied for this topic
    }
    // else: not enough data
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
    public string? CleanNameArticles { get; set; }
    public string? AltName { get; set; }
    public string? Genre { get; set; }
    public string? ReleaseYear { get; set; }
    public int? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? PosterImage { get; set; }
    public string? BackgroundImage { get; set; }
    public string? Overview { get; set; }
    public string? Review { get; set; }
    public int NumRatings { get; set; }
    public int Verified { get; set; }
    public int PosterVerified { get; set; }
    public int BackgroundVerified { get; set; }
    public bool StaffVerified { get; set; }
    public int Adult { get; set; }
    public int VerifyAttempts { get; set; }
    public string? Stats { get; set; }
    public int ItemTypeId { get; set; }
    public DtddItemType? ItemType { get; set; }
    public int MinStaffIndex1 { get; set; }
    public int MinStaffIndex2 { get; set; }
    public int MaxStaffIndex1 { get; set; }
    public int MaxStaffIndex2 { get; set; }
}

public class DtddItemType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Verb { get; set; }
    public string? PastTenseVerb { get; set; }
    public string? Index1 { get; set; }
    public string? Index2 { get; set; }
    public string? Position1 { get; set; }
    public string? Position2 { get; set; }
    public string? Position3 { get; set; }
    public string? SectionName { get; set; }
    public string? OverviewName { get; set; }
}

public class DtddMediaDetails
{
    public DtddMediaItem Item { get; set; } = new();
    public List<DtddTopicItemStat> TopicItemStats { get; set; } = new();
    public List<DtddAllGroup> AllGroups { get; set; } = new();
    public string? SmartPageTitle { get; set; }
    public string? SmartPageDescription { get; set; }
    public int? Index1 { get; set; }
    public int? Index2 { get; set; }
    public int NumYes { get; set; }
    public int NumNo { get; set; }
}

public class DtddAllGroup
{
    public string Name { get; set; } = string.Empty;
    public List<DtddTopicItemStat> Topics { get; set; } = new();
}

public class DtddTopicItemStat
{
    public int TopicItemId { get; set; }
    public int YesSum { get; set; }
    public int NoSum { get; set; }
    public int NumComments { get; set; }
    public int TopicId { get; set; }
    public int ItemId { get; set; }
    public int? RatingId { get; set; }
    public string? CommentUserIds { get; set; }
    public int VoteSum { get; set; }
    public string? Comment { get; set; }
    public int IsAnonymous { get; set; }
    public string? Username { get; set; }
    public int? UserId { get; set; }
    public int IsYes { get; set; }
    public int Index1 { get; set; }
    public int Index2 { get; set; }
    public int RatingIndex1 { get; set; }
    public int RatingIndex2 { get; set; }
    public bool HasUserComment { get; set; }
    public bool IsFavorite { get; set; }
    public List<DtddComment>? Comments { get; set; }
    public DtddTopic? Topic { get; set; }
    public string? DoesName { get; set; }
    public DtddTopicCategory? TopicCategory { get; set; }
    public string? Slug { get; set; }
    // Fields present on topic-slug browse responses
    public string? ItemName { get; set; }
    public string? ItemPosterImage { get; set; }
    public string? ItemBackgroundImage { get; set; }
    public string? ItemCleanName { get; set; }
    public string? ReleaseYear { get; set; }
    public string? ItemTypeName { get; set; }
    public string? ItemTypeSlug { get; set; }
    public int? ItemTypeId { get; set; }
}

public class DtddComment
{
    public int Id { get; set; }
    public int VoteSum { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int Index1 { get; set; }
    public int Index2 { get; set; }
    public DtddCommentUser? User { get; set; }
}

public class DtddCommentUser
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class DtddTopic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NotName { get; set; }
    public string? SurvivesName { get; set; }
    public string? Keywords { get; set; }
    public string? DoesName { get; set; }
    public string? ListName { get; set; }
    public string? Description { get; set; }
    public string? Subtitle { get; set; }
    public string? SubtitleText { get; set; }
    public string? SubtitleUrl { get; set; }
    public string? Image { get; set; }
    public int Ordering { get; set; }
    public int DemandOrder { get; set; }
    public bool IsSpoiler { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsVisible { get; set; }
    public string? SmmwDescription { get; set; }
    public int? LegacyId { get; set; }
    public int Supporters { get; set; }
    public int? TopicCategoryId { get; set; }
    public int? TopicSubCategoryId { get; set; }
    public DtddTopicCategory? TopicCategory { get; set; }
    public string? Slug { get; set; }
    public bool? IsFavorite { get; set; }
}

public class DtddTopicCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DtddTopicBrowseResponse
{
    public DtddTopic Topic { get; set; } = new();
    public List<DtddTopicItemStat> TopicItemStats { get; set; } = new();
    public List<DtddStatsCount> TopicItemStatsCount { get; set; } = new();
    public string ItemTypeId { get; set; } = "all";
    public string ItemTypeName { get; set; } = "Media";
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public List<DtddItemType> ItemTypes { get; set; } = new();
    public string YesNo { get; set; } = "yes";
}

public class DtddStatsCount
{
    public int Count { get; set; }
}

/// <summary>
/// Represents the parsed `stats` JSON string from search results.
/// </summary>
public class DtddItemStats
{
    public Dictionary<string, DtddQuickTopicResult> Topics { get; set; } = new();
}

public class DtddQuickTopicResult
{
    public int DefinitelyYes { get; set; }
    public int DefinitelyNo { get; set; }
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

## Plugin-Relevant Optimization: Using `stats` for Bulk Lookups

For a Jellyfin plugin scanning an entire library, the `stats` field in search results eliminates the need for individual `/media/{id}` calls in many cases:

1. Search by IMDB ID: `GET /dddsearch?imdb={imdbId}`
2. Parse the `stats` JSON string from the first result
3. Cross-reference topic IDs against the `/categories` response
4. Only call `/media/{id}` when you need full comment text or detailed vote counts

This reduces API calls from 2 per item (search + media) to 1 per item + 1 global categories call.

---

## References

- Official (limited): https://www.doesthedogdie.com/api
- TypeScript wrapper: https://github.com/jayshoo/doesthedogdie-api
