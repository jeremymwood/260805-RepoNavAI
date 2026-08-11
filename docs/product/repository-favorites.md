# Repository favorites and list disclosure

Repository favorites are private per-user preferences scoped to an organization and repository. They never change shared repository metadata or another member's ordering.

The repository API returns bounded pages of at most 50 items. The workspace fetches 10 at a time, orders the current user's favorites first, then orders by owner, name, and repository ID. The overview initially renders four cards to preserve the first viewport and reveals fetched results only after an explicit **Show more** action. **Show less** returns the view to the initial card count without discarding already fetched pages.

Favorites are inaccessible as soon as organization membership is lost. Deleting the user, organization, or registered repository removes the preference through database cascades. Removing membership alone retains the private preference so it is restored if the same user later regains access; it cannot be read or changed while access is absent.
