# Activity IDs — cross-family / shared ExternalId

## Problem

`ActivityAlbum.ExternalId` (`act_...`) is only unique per family. A viewer may see **multiple albums** with the same `externalId` (own + friend-shared).

## API contract (0928+)

| Field | Meaning |
|--------|---------|
| `id` / `activityAlbumId` | Global row id (`activity_albums.id` GUID string) — **preferred** for mutations and photos when available |
| `externalId` | Legacy/display `act_...` (may collide for a viewer) |
| `familyId` | Owning family |
| `creatorUserId` | Album owner account id — use with `externalId` to disambiguate |

## Resolve rules

All routes that accept `{activityId}` accept **GUID** or **`externalId`**.

When multiple accessible rows match:

1. Optional **`creatorUserId`** (query or upsert body on PUT) selects `CreatorUserId == creatorUserId`.
2. Otherwise **`GET .../photos`** returns **409** if still ambiguous.
3. Otherwise (upload, in-progress link, etc.) prefer **`creatorUserId == viewer`**, then newest row.

## Photos

```
GET /api/v1/activities/{activityId}/photos?limit=200&creatorUserId={uuid}
```

- `{activityId}`: GUID **or** `externalId`
- **`creatorUserId`**: required when the viewer has multiple accessible albums with the same `externalId` (otherwise 409)

## App identity (0930live)

`activeActivityIdentityKey = "${externalId or id}|${creatorUserId}"`

Use list/active-today nested **`activity`** fields (`title`, `type`, `creatorUserId`, `viewerIsParticipant`) as source of truth — do not merge by `externalId` alone.
