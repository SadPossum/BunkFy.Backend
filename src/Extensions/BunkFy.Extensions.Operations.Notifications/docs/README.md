# Operations Notifications Extension

This extension converts selected BunkFy product events into already-addressed
GMA notification requests. Source modules own business facts, BunkFy owns
recipient and content policy, Organizations authoritatively filters active
membership, and Notifications owns generic inbox persistence and delivery.

The executable personal-data catalogue is
[personal-data-catalog.v1.json](personal-data-catalog.v1.json). Its deterministic
resolved view is
[personal-data-inventory.v1.md](personal-data-inventory.v1.md).

Payloads are a closed set of typed navigation records. Product source events may
contain richer audit or workflow data, but the inbox receives only the minimal
resource identifiers and dates needed to understand or open the affected item.

Every BunkFy-addressed copy also carries an opaque reference to the
authoritative Staff record for its recipient. The reference survives Auth
account relinking and lets this extension contribute the exact inbox history to
tenant-scoped Staff access export and anonymisation. Export accepts only the
current source-module, notification-name, version, and payload combinations;
recipient Auth subjects, read state, and delivery attempts are excluded.

GMA Notifications owns generic inbox persistence, paging, delivery-lease
coordination, close receipts, and replay suppression. This extension adapts
those primitives to BunkFy's Staff and reservation Data Rights coordinates; no
BunkFy policy is implemented in GMA.
